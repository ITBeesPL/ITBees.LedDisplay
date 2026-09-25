using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.Models;

public enum LedDisplayProtocol
{
    /// <summary>Elitel line protocol over TCP (SET/CLEAR/COLOR/FONT/BRIGHT/PIX).</summary>
    Elitel = 0,
}

/// <summary>One physical board: where it is reachable and how big its screen is.</summary>
public sealed record LedDisplayDefinition
{
    /// <summary>Stable identifier the host application addresses the board by.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = ElitelCommands.DefaultPort;

    /// <summary>Screen width in pixels (a single P10 module is 32x16).</summary>
    public int Width { get; set; } = 64;

    public int Height { get; set; } = 32;
    public LedDisplayProtocol Protocol { get; set; } = LedDisplayProtocol.Elitel;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("LED display needs an Id");
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ArgumentException($"LED display '{Id}' has no Host");
        }

        if (Port is < 1 or > 65535)
        {
            throw new ArgumentException($"LED display '{Id}' has an invalid port {Port}");
        }

        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentException($"LED display '{Id}' has an invalid size {Width}x{Height}");
        }
    }

    public override string ToString() => $"{Id} ({Host}:{Port})";
}
