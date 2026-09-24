using ITBees.LedDisplay.Models;
using ITBees.LedDisplay.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITBees.LedDisplay.Services;

public sealed class LedDisplayManager : ILedDisplayManager
{
    private readonly ILedDisplayClientFactory _ledDisplayClientFactory;
    private readonly LedDisplayOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LedDisplayManager> _logger;
    private readonly object _configurationLock = new();
    // Replaced as a whole on reconfiguration and never mutated afterwards, so readers need no lock.
    private volatile Dictionary<string, DisplaySlot> _slots = new(StringComparer.OrdinalIgnoreCase);

    public LedDisplayManager(
        IOptions<LedDisplayOptions> options,
        ILedDisplayClientFactory ledDisplayClientFactory,
        TimeProvider timeProvider,
        ILogger<LedDisplayManager> logger)
    {
        _options = options.Value;
        _ledDisplayClientFactory = ledDisplayClientFactory;
        _timeProvider = timeProvider;
        _logger = logger;
        Configure(_options.Displays);
    }

    public event EventHandler<LedDisplayStatusChangedEventArgs>? StatusChanged;

    public IReadOnlyCollection<LedDisplayDefinition> Displays => _slots.Values.Select(x => x.Client.Definition).ToList();

    public void Configure(IEnumerable<LedDisplayDefinition> displays)
    {
        var definitions = displays.ToList();
        foreach (var definition in definitions)
        {
            definition.Validate();
        }

        var duplicate = definitions.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(x => x.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"LED display id '{duplicate.Key}' is used more than once");
        }

        lock (_configurationLock)
        {
            var slots = new Dictionary<string, DisplaySlot>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in definitions)
            {
                if (_slots.TryGetValue(definition.Id, out var existing) && existing.Client.Definition == definition)
                {
                    slots[definition.Id] = existing;
                    continue;
                }

                var slot = new DisplaySlot(_ledDisplayClientFactory.Create(definition with { }));
                if (existing?.DesiredFrame is { } desired && desired.Width == definition.Width && desired.Height == definition.Height)
                {
                    slot.DesiredFrame = desired;
                }

                slots[definition.Id] = slot;
                _logger.LogInformation("LED display {Display} configured ({Width}x{Height})", definition, definition.Width, definition.Height);
            }

            foreach (var removed in _slots.Keys.Where(x => slots.ContainsKey(x) == false))
            {
                _logger.LogInformation("LED display {DisplayId} removed", removed);
            }

            _slots = slots;
        }
    }

    public async Task<LedDisplayStatus> ShowAsync(string displayId, LedFrame frame, CancellationToken cancellationToken = default)
    {
        var slot = GetSlot(displayId);
        var definition = slot.Client.Definition;
        if (frame.Width != definition.Width || frame.Height != definition.Height)
        {
            throw new ArgumentException(
                $"Frame is {frame.Width}x{frame.Height} but LED display '{definition.Id}' is {definition.Width}x{definition.Height}");
        }

        slot.DesiredFrame = frame;
        await slot.Gate.WaitAsync(cancellationToken);
        try
        {
            // A newer frame arrived while this one was waiting - its own call sends it.
            if (ReferenceEquals(slot.DesiredFrame, frame) == false)
            {
                return slot.Snapshot();
            }

            if (slot.State == LedDisplayState.Online && slot.ShownFrame?.Signature == frame.Signature)
            {
                return slot.Snapshot();
            }

            await Send(slot, frame, cancellationToken);
            return slot.Snapshot();
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    public async Task<LedDisplayStatus> RefreshAsync(string displayId, CancellationToken cancellationToken = default)
    {
        var slot = GetSlot(displayId);
        await slot.Gate.WaitAsync(cancellationToken);
        try
        {
            if (slot.DesiredFrame is { } desired)
            {
                await Send(slot, desired, cancellationToken);
            }
            else
            {
                await Probe(slot, cancellationToken);
            }

            return slot.Snapshot();
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    public LedDisplayStatus? GetStatus(string displayId) =>
        _slots.TryGetValue(displayId, out var slot) ? slot.Snapshot() : null;

    public IReadOnlyList<LedDisplayStatus> GetStatuses() => _slots.Values.Select(x => x.Snapshot()).ToList();

    public Task RunMaintenanceAsync(CancellationToken cancellationToken = default) =>
        Task.WhenAll(_slots.Values.Select(x => Maintain(x, cancellationToken)));

    private async Task Maintain(DisplaySlot slot, CancellationToken cancellationToken)
    {
        // A board busy with a send right now needs no maintenance this round.
        if (await slot.Gate.WaitAsync(0, cancellationToken) == false)
        {
            return;
        }

        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var desired = slot.DesiredFrame;
            var sinceAttempt = slot.LastAttemptUtc.HasValue ? now - slot.LastAttemptUtc.Value : TimeSpan.MaxValue;

            if (desired != null && slot.ShownFrame?.Signature != desired.Signature)
            {
                if (sinceAttempt >= _options.RetryInterval)
                {
                    await Send(slot, desired, cancellationToken);
                }

                return;
            }

            if (desired != null
                && _options.RefreshInterval > TimeSpan.Zero
                && slot.LastFullSendUtc.HasValue
                && now - slot.LastFullSendUtc.Value >= _options.RefreshInterval)
            {
                await Send(slot, desired, cancellationToken);
                return;
            }

            // A failed probe forgets the shown frame, so after an outage the branch above sends the whole
            // frame again - the board may have been power-cycled and lost its content.
            var probeInterval = slot.State == LedDisplayState.Online ? _options.ProbeInterval : _options.RetryInterval;
            if (_options.ProbeInterval > TimeSpan.Zero && sinceAttempt >= probeInterval)
            {
                await Probe(slot, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "LED display {Display} maintenance failed", slot.Client.Definition);
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    private async Task Send(DisplaySlot slot, LedFrame frame, CancellationToken cancellationToken)
    {
        slot.LastAttemptUtc = _timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await slot.Client.ShowAsync(frame, cancellationToken);
            slot.ShownFrame = frame;
            slot.LastFullSendUtc = slot.LastAttemptUtc;
            MarkSuccess(slot);
            _logger.LogInformation("LED display {Display} shows '{Description}'", slot.Client.Definition, frame.Description);
        }
        catch (LedDisplayException e)
        {
            // A frame that broke off halfway leaves the screen in an unknown state.
            slot.ShownFrame = null;
            MarkFailure(slot, e);
        }
    }

    private async Task Probe(DisplaySlot slot, CancellationToken cancellationToken)
    {
        slot.LastAttemptUtc = _timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await slot.Client.ProbeAsync(slot.ShownFrame, cancellationToken);
            MarkSuccess(slot);
        }
        catch (LedDisplayException e)
        {
            slot.ShownFrame = null;
            MarkFailure(slot, e);
        }
    }

    private void MarkSuccess(DisplaySlot slot)
    {
        var previous = slot.State;
        slot.State = LedDisplayState.Online;
        slot.LastSuccessUtc = slot.LastAttemptUtc;
        slot.ConsecutiveFailures = 0;
        RaiseIfChanged(slot, previous);
    }

    private void MarkFailure(DisplaySlot slot, LedDisplayException exception)
    {
        var previous = slot.State;
        slot.State = LedDisplayState.Offline;
        slot.LastFailureUtc = slot.LastAttemptUtc;
        slot.LastError = exception.Message;
        slot.ConsecutiveFailures++;

        // Log the first failure loudly, the repeats of an ongoing outage quietly.
        if (slot.ConsecutiveFailures == 1)
        {
            _logger.LogWarning("LED display {Display} failed: {Error}", slot.Client.Definition, exception.Message);
        }
        else
        {
            _logger.LogDebug("LED display {Display} still failing ({Count}): {Error}",
                slot.Client.Definition, slot.ConsecutiveFailures, exception.Message);
        }

        RaiseIfChanged(slot, previous);
    }

    private void RaiseIfChanged(DisplaySlot slot, LedDisplayState previous)
    {
        if (previous == slot.State)
        {
            return;
        }

        try
        {
            StatusChanged?.Invoke(this, new LedDisplayStatusChangedEventArgs(previous, slot.Snapshot()));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "LED display {Display} status handler failed", slot.Client.Definition);
        }
    }

    private DisplaySlot GetSlot(string displayId) =>
        _slots.TryGetValue(displayId, out var slot)
            ? slot
            : throw new KeyNotFoundException($"LED display '{displayId}' is not configured");

    private sealed class DisplaySlot
    {
        private LedFrame? _desiredFrame;

        public DisplaySlot(ILedDisplayClient client)
        {
            Client = client;
        }

        public ILedDisplayClient Client { get; }
        public SemaphoreSlim Gate { get; } = new(1, 1);

        /// <summary>Written outside the gate (latest wins), hence volatile.</summary>
        public LedFrame? DesiredFrame
        {
            get => Volatile.Read(ref _desiredFrame);
            set => Volatile.Write(ref _desiredFrame, value);
        }

        public LedFrame? ShownFrame { get; set; }
        public LedDisplayState State { get; set; }
        public DateTime? LastAttemptUtc { get; set; }
        public DateTime? LastFullSendUtc { get; set; }
        public DateTime? LastSuccessUtc { get; set; }
        public DateTime? LastFailureUtc { get; set; }
        public string? LastError { get; set; }
        public int ConsecutiveFailures { get; set; }

        public LedDisplayStatus Snapshot()
        {
            var definition = Client.Definition;
            var desired = DesiredFrame;
            var shown = ShownFrame;
            return new LedDisplayStatus
            {
                DisplayId = definition.Id,
                Name = definition.Name,
                Host = definition.Host,
                Port = definition.Port,
                State = State,
                LastSuccessUtc = LastSuccessUtc,
                LastFailureUtc = LastFailureUtc,
                LastError = LastError,
                ConsecutiveFailures = ConsecutiveFailures,
                ShownDescription = shown?.Description,
                DesiredDescription = desired?.Description,
                IsFramePending = desired != null && shown?.Signature != desired.Signature,
            };
        }
    }
}
