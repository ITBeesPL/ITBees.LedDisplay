namespace ITBees.LedDisplay.Models;

/// <summary>The board could not be reached or did not answer in time.</summary>
public class LedDisplayException : Exception
{
    public LedDisplayException(string displayId, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        DisplayId = displayId;
    }

    public string DisplayId { get; }
}

/// <summary>The board answered a command with something other than <c>OK</c>.</summary>
public sealed class LedDisplayCommandRejectedException : LedDisplayException
{
    public LedDisplayCommandRejectedException(string displayId, string command, string response)
        : base(displayId, $"LED display '{displayId}' rejected '{command}' with '{response}'")
    {
        Command = command;
        Response = response;
    }

    public string Command { get; }
    public string Response { get; }
}
