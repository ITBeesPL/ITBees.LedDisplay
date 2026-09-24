using ITBees.LedDisplay.Models;
using ITBees.LedDisplay.Rendering;

namespace ITBees.LedDisplay.Services;

/// <summary>
/// Registry of boards: remembers what each one should show, skips sends that would change nothing,
/// retries boards that are offline, re-sends the content periodically and reports online/offline
/// transitions. Register as a singleton; <see cref="LedDisplayMaintenanceService"/> drives the
/// background part.
/// </summary>
public interface ILedDisplayManager
{
    IReadOnlyCollection<LedDisplayDefinition> Displays { get; }

    /// <summary>
    /// Replaces the set of boards. Boards whose definition did not change keep their state; a changed
    /// board starts over (and keeps its desired frame if the screen size stayed the same).
    /// </summary>
    void Configure(IEnumerable<LedDisplayDefinition> displays);

    /// <summary>
    /// Makes the board show <paramref name="frame"/>. Nothing is sent when the board is online and already
    /// shows an identical frame, or when a newer frame arrives while this one waits for the board.
    /// Failures do not throw - the returned status says what happened and the frame is retried in the
    /// background.
    /// </summary>
    Task<LedDisplayStatus> ShowAsync(string displayId, LedFrame frame, CancellationToken cancellationToken = default);

    /// <summary>Sends the desired frame again even if the board is believed to show it already.</summary>
    Task<LedDisplayStatus> RefreshAsync(string displayId, CancellationToken cancellationToken = default);

    LedDisplayStatus? GetStatus(string displayId);
    IReadOnlyList<LedDisplayStatus> GetStatuses();

    /// <summary>Raised on every change of <see cref="LedDisplayStatus.State"/>. Handlers must not block.</summary>
    event EventHandler<LedDisplayStatusChangedEventArgs>? StatusChanged;

    /// <summary>One pass of retries, probes and periodic refreshes for boards that are due.</summary>
    Task RunMaintenanceAsync(CancellationToken cancellationToken = default);
}
