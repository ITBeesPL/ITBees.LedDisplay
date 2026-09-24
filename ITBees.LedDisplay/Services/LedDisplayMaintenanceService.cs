using ITBees.LedDisplay.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITBees.LedDisplay.Services;

/// <summary>Runs <see cref="ILedDisplayManager.RunMaintenanceAsync"/> on a short tick.</summary>
public sealed class LedDisplayMaintenanceService : BackgroundService
{
    private readonly ILedDisplayManager _ledDisplayManager;
    private readonly ILogger<LedDisplayMaintenanceService> _logger;
    private readonly TimeSpan _tick;

    public LedDisplayMaintenanceService(
        ILedDisplayManager ledDisplayManager,
        IOptions<LedDisplayOptions> options,
        ILogger<LedDisplayMaintenanceService> logger)
    {
        _ledDisplayManager = ledDisplayManager;
        _logger = logger;

        // Fine enough for the shortest interval, but never a busy loop.
        var shortest = new[] { options.Value.RetryInterval, options.Value.ProbeInterval }
            .Where(x => x > TimeSpan.Zero)
            .DefaultIfEmpty(TimeSpan.FromSeconds(5))
            .Min();
        _tick = TimeSpan.FromTicks(Math.Clamp(shortest.Ticks / 3, TimeSpan.FromSeconds(1).Ticks, TimeSpan.FromSeconds(5).Ticks));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_tick);
        do
        {
            try
            {
                await _ledDisplayManager.RunMaintenanceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "LED display maintenance pass failed");
            }
        }
        while (await WaitForNextTick(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
