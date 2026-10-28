namespace ITBees.LedDisplay.Protocol;

/// <summary>
/// Fixed-width fonts built into the controller. The value is the <c>FONT;n</c> parameter. The Elitel
/// controller has fonts 0..4, named after their character cell (width x height in pixels); the board
/// behind the DispApp RS485 converter also has 5..9, whose cells are controller specific - describe
/// them with a <see cref="LedFontSet"/>.
/// </summary>
public enum LedFont
{
    Font4x6 = 0,
    Font6x8 = 1,
    Font8x8 = 2,
    Font8x16 = 3,
    Font12x24 = 4,
    Font5 = 5,
    Font6 = 6,
    Font7 = 7,
    Font8 = 8,
    Font9 = 9,
}

/// <summary>Cell sizes of the Elitel fonts - shorthand for <see cref="LedFontSet.Elitel"/>.</summary>
public static class LedFontMetrics
{
    /// <summary>The Elitel fonts ordered from the biggest cell to the smallest - the order auto-fitting tries them in.</summary>
    public static IReadOnlyList<LedFont> LargestFirst => LedFontSet.Elitel.LargestFirst;

    public static int CellWidth(LedFont font) => LedFontSet.Elitel.CellWidth(font);

    public static int CellHeight(LedFont font) => LedFontSet.Elitel.CellHeight(font);

    /// <summary>Width in pixels of a single line of text.</summary>
    public static int MeasureWidth(string text, LedFont font) => LedFontSet.Elitel.MeasureWidth(text, font);
}
