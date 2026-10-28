using System.Globalization;

namespace ITBees.LedDisplay.Protocol;

/// <summary>Character cell of one controller font, in pixels.</summary>
public sealed record LedFontSize(LedFont Font, int CellWidth, int CellHeight)
{
    public override string ToString() => $"{(int)Font}:{CellWidth}x{CellHeight}";
}

/// <summary>
/// The fonts a controller has and their character cells. Text is fitted with the set of the board it is
/// rendered for, so a controller with extra fonts (FONT;5..9 on the boards behind the DispApp RS485
/// converter) can use them, while a plain Elitel board never gets a font it does not have.
/// </summary>
public sealed class LedFontSet
{
    private readonly Dictionary<LedFont, LedFontSize> _sizes = new();

    /// <summary>Builds a set; when a font is listed twice the later size wins (see <see cref="With"/>).</summary>
    public LedFontSet(IEnumerable<LedFontSize> fonts)
    {
        foreach (var size in fonts)
        {
            if (Enum.IsDefined(size.Font) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(fonts), size.Font, "The controller has fonts 0..9");
            }

            if (size.CellWidth <= 0 || size.CellHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fonts), size, $"Font {(int)size.Font} needs a positive cell size");
            }

            _sizes[size.Font] = size;
        }

        if (_sizes.Count == 0)
        {
            throw new ArgumentException("A font set needs at least one font", nameof(fonts));
        }

        Fonts = _sizes.Values.OrderBy(x => (int)x.Font).ToList();
        LargestFirst = _sizes.Values
            .OrderByDescending(x => x.CellHeight)
            .ThenByDescending(x => x.CellWidth)
            .ThenBy(x => (int)x.Font)
            .Select(x => x.Font)
            .ToList();
    }

    /// <summary>The five fonts of the Elitel controller (FONT;0..4).</summary>
    public static LedFontSet Elitel { get; } = new(new[]
    {
        new LedFontSize(LedFont.Font4x6, 4, 6),
        new LedFontSize(LedFont.Font6x8, 6, 8),
        new LedFontSize(LedFont.Font8x8, 8, 8),
        new LedFontSize(LedFont.Font8x16, 8, 16),
        new LedFontSize(LedFont.Font12x24, 12, 24),
    });

    /// <summary>The fonts ordered by number.</summary>
    public IReadOnlyList<LedFontSize> Fonts { get; }

    /// <summary>The fonts from the biggest cell to the smallest - the order text fitting tries them in.</summary>
    public IReadOnlyList<LedFont> LargestFirst { get; }

    public bool Contains(LedFont font) => _sizes.ContainsKey(font);

    public int CellWidth(LedFont font) => Size(font).CellWidth;

    public int CellHeight(LedFont font) => Size(font).CellHeight;

    /// <summary>Width in pixels of a single line of text.</summary>
    public int MeasureWidth(string text, LedFont font) => text.Length * CellWidth(font);

    /// <summary>This set with the given fonts added, or resized when the set already has them.</summary>
    public LedFontSet With(IEnumerable<LedFontSize> fonts) => new(Fonts.Concat(fonts));

    /// <summary>
    /// Reads font sizes written as <c>"5:16x32, 6:24x32"</c>: the FONT number, a colon and the cell
    /// width x height in pixels; entries separated with commas or semicolons. Blank text gives no fonts.
    /// </summary>
    public static IReadOnlyList<LedFontSize> ParseSizes(string? text)
    {
        var sizes = new List<LedFontSize>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return sizes;
        }

        foreach (var entry in text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var numberAndCell = entry.Split(':', StringSplitOptions.TrimEntries);
            var cell = numberAndCell.Length == 2
                ? numberAndCell[1].Split(new[] { 'x', 'X', '×' }, StringSplitOptions.TrimEntries)
                : Array.Empty<string>();

            if (cell.Length != 2
                || int.TryParse(numberAndCell[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number) == false
                || int.TryParse(cell[0], NumberStyles.None, CultureInfo.InvariantCulture, out var width) == false
                || int.TryParse(cell[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height) == false
                || Enum.IsDefined((LedFont)number) == false
                || width <= 0
                || height <= 0)
            {
                throw new FormatException($"'{entry}' is not a font size - expected e.g. \"5:16x32\" (font 0..9, cell width x height)");
            }

            sizes.Add(new LedFontSize((LedFont)number, width, height));
        }

        return sizes;
    }

    public override string ToString() => string.Join(", ", Fonts);

    private LedFontSize Size(LedFont font) =>
        _sizes.TryGetValue(font, out var size)
            ? size
            : throw new ArgumentOutOfRangeException(nameof(font), font, $"Font {(int)font} is not in the board's font set ({this})");
}
