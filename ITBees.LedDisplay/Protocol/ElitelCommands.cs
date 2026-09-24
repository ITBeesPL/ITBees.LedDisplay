namespace ITBees.LedDisplay.Protocol;

/// <summary>
/// Builders for the Elitel line protocol (TCP, port 3000 by default). Every command is one line
/// ended with <c>\n</c> and the controller answers <c>OK\n</c>.
/// </summary>
/// <remarks>
/// Coordinates are pixels from the top-left corner; note that the wire order is <c>y;x</c>.
/// <c>PIX</c> is not part of the vendor's written description - it comes from the vendor's test
/// script (<c>pix;0;0;7</c>, <c>pix;31;0;6</c>, <c>pix;31;63;4</c> light the corners of a 64x32 board).
/// </remarks>
public static class ElitelCommands
{
    public const int DefaultPort = 3000;

    /// <summary>
    /// <c>SET;y;x;"text"</c> - draws the text with the current font and colour, the position being
    /// the top-left corner of the first character. A character that does not fit on the screen as
    /// a whole is not drawn at all.
    /// </summary>
    public static LedCommand SetText(int x, int y, string text)
    {
        EnsureCoordinate(x, nameof(x));
        EnsureCoordinate(y, nameof(y));
        return new LedCommand($"SET;{y};{x};\"{ElitelTextEncoding.Sanitize(text)}\"");
    }

    /// <summary><c>CLEAR</c> - blanks the whole screen.</summary>
    public static LedCommand Clear() => new("CLEAR");

    /// <summary>
    /// <c>COLOR;c</c> - takes effect immediately and recolours everything already on the screen,
    /// as well as the texts drawn afterwards.
    /// </summary>
    public static LedCommand Color(LedColor color)
    {
        if (color is < LedColor.Red or > LedColor.White)
        {
            throw new ArgumentOutOfRangeException(nameof(color), color, "COLOR accepts 1..7");
        }

        return new LedCommand($"COLOR;{(int)color}");
    }

    /// <summary><c>FONT;n</c> - selects the font for the texts drawn afterwards.</summary>
    public static LedCommand Font(LedFont font)
    {
        if (Enum.IsDefined(font) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(font), font, "Unknown font");
        }

        return new LedCommand($"FONT;{(int)font}");
    }

    /// <summary><c>BRIGHT;n</c> - 1..9 fixed, <see cref="LedBrightness.Auto"/> (10) automatic.</summary>
    public static LedCommand Brightness(int level)
    {
        if (LedBrightness.IsValid(level) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "BRIGHT accepts 1..10");
        }

        return new LedCommand($"BRIGHT;{level}");
    }

    /// <summary><c>PIX;y;x;c</c> - lights a single pixel (<see cref="LedColor.Off"/> switches it off).</summary>
    public static LedCommand Pixel(int x, int y, LedColor color)
    {
        EnsureCoordinate(x, nameof(x));
        EnsureCoordinate(y, nameof(y));
        if (color is < LedColor.Off or > LedColor.White)
        {
            throw new ArgumentOutOfRangeException(nameof(color), color, "PIX accepts colours 0..7");
        }

        return new LedCommand($"PIX;{y};{x};{(int)color}");
    }

    private static void EnsureCoordinate(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Coordinates start at 0");
        }
    }
}
