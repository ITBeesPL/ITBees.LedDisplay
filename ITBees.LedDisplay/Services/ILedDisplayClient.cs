using ITBees.LedDisplay.Models;
using ITBees.LedDisplay.Protocol;
using ITBees.LedDisplay.Rendering;

namespace ITBees.LedDisplay.Services;

/// <summary>Talks to one board. Implementations serialise calls - a board handles one command at a time.</summary>
public interface ILedDisplayClient
{
    LedDisplayDefinition Definition { get; }

    /// <summary>Draws the frame from a blank screen. Throws <see cref="LedDisplayException"/> on failure.</summary>
    Task ShowAsync(LedFrame frame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks that the board answers, without changing what it shows. <paramref name="currentFrame"/>
    /// is what the board is believed to display - the probe repeats its colour.
    /// </summary>
    Task ProbeAsync(LedFrame? currentFrame, CancellationToken cancellationToken = default);

    /// <summary>Sends raw commands in order, each confirmed with <c>OK</c> before the next one goes out.</summary>
    Task ExecuteAsync(IReadOnlyList<LedCommand> commands, CancellationToken cancellationToken = default);
}

public interface ILedDisplayClientFactory
{
    ILedDisplayClient Create(LedDisplayDefinition definition);
}
