namespace ITBees.LedDisplay.Protocol;

/// <summary>
/// Fixed-width fonts built into the Elitel controller. The value is the <c>FONT;n</c> parameter,
/// the name is the character cell (width x height in pixels) a glyph is drawn in.
/// </summary>
public enum LedFont
{
    Font4x6 = 0,
    Font6x8 = 1,
    Font8x8 = 2,
    Font8x16 = 3,
    Font12x24 = 4,
}

public static class LedFontMetrics
{
    /// <summary>All fonts ordered from the biggest cell to the smallest - the order auto-fitting tries them in.</summary>
    public static IReadOnlyList<LedFont> LargestFirst { get; } = new[]
    {
        LedFont.Font12x24,
        LedFont.Font8x16,
        LedFont.Font8x8,
        LedFont.Font6x8,
        LedFont.Font4x6,
    };

    public static int CellWidth(LedFont font) => font switch
    {
        LedFont.Font4x6 => 4,
        LedFont.Font6x8 => 6,
        LedFont.Font8x8 => 8,
        LedFont.Font8x16 => 8,
        LedFont.Font12x24 => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(font), font, "Unknown font"),
    };

    public static int CellHeight(LedFont font) => font switch
    {
        LedFont.Font4x6 => 6,
        LedFont.Font6x8 => 8,
        LedFont.Font8x8 => 8,
        LedFont.Font8x16 => 16,
        LedFont.Font12x24 => 24,
        _ => throw new ArgumentOutOfRangeException(nameof(font), font, "Unknown font"),
    };

    /// <summary>Width in pixels of a single line of text.</summary>
    public static int MeasureWidth(string text, LedFont font) => text.Length * CellWidth(font);
}
