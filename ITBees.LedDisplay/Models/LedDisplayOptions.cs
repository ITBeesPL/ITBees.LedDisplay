namespace ITBees.LedDisplay.Models;

public class LedDisplayOptions
{
    public const string SectionName = "LedDisplays";

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>How long a single command may wait for its <c>OK</c>.</summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Liveness check of an online board - a harmless command repeating the current colour.
    /// <see cref="TimeSpan.Zero"/> disables probing.
    /// </summary>
    public TimeSpan ProbeInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Full resend of the current frame, which restores the content after the board lost power.
    /// Each resend clears the screen for a moment. <see cref="TimeSpan.Zero"/> disables it.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Pause between attempts at an offline board.</summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Boards known at startup; the host may replace the set later with <c>ILedDisplayManager.Configure</c>.</summary>
    public List<LedDisplayDefinition> Displays { get; set; } = new();
}
