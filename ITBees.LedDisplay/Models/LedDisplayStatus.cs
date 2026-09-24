namespace ITBees.LedDisplay.Models;

public enum LedDisplayState
{
    /// <summary>Nothing has been sent to the board yet.</summary>
    Unknown = 0,
    Online = 1,
    Offline = 2,
}

/// <summary>Point-in-time snapshot of a board, safe to hand out.</summary>
public sealed record LedDisplayStatus
{
    public string DisplayId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public LedDisplayState State { get; init; }
    public DateTime? LastSuccessUtc { get; init; }
    public DateTime? LastFailureUtc { get; init; }
    public string? LastError { get; init; }
    public int ConsecutiveFailures { get; init; }

    /// <summary>What the board is confirmed to show (null when unknown, e.g. after a failed send).</summary>
    public string? ShownDescription { get; init; }

    /// <summary>What the board should show; differs from <see cref="ShownDescription"/> while a send is pending.</summary>
    public string? DesiredDescription { get; init; }

    public bool IsFramePending { get; init; }
}

public sealed class LedDisplayStatusChangedEventArgs : EventArgs
{
    public LedDisplayStatusChangedEventArgs(LedDisplayState previousState, LedDisplayStatus status)
    {
        PreviousState = previousState;
        Status = status;
    }

    public LedDisplayState PreviousState { get; }
    public LedDisplayStatus Status { get; }
}
