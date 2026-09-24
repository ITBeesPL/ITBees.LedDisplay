using System.Text;
using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.DeviceEmulator;

/// <summary>A drawn character: its cell on the screen and the glyph.</summary>
public readonly record struct EmulatedGlyph(int X, int Y, char Character, LedFont Font);

/// <summary>
/// State of an emulated Elitel board. Follows the documented rules: a character that does not fit
/// the screen as a whole is not drawn, <c>COLOR</c> recolours everything already shown.
/// </summary>
public sealed class EmulatedScreen
{
    private readonly object _lock = new();
    private readonly List<EmulatedGlyph> _glyphs = new();
    private readonly LedColor[,] _pixels;

    public EmulatedScreen(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new LedColor[height, width];
    }

    public int Width { get; }
    public int Height { get; }
    public LedColor Color { get; private set; } = LedColor.Red;
    public LedFont Font { get; private set; } = LedFont.Font6x8;
    public int Brightness { get; private set; } = LedBrightness.Auto;

    public IReadOnlyList<EmulatedGlyph> Glyphs
    {
        get
        {
            lock (_lock)
            {
                return _glyphs.ToList();
            }
        }
    }

    /// <summary>The texts on the screen, glyphs on the same row joined in reading order.</summary>
    public IReadOnlyList<string> TextLines
    {
        get
        {
            lock (_lock)
            {
                return _glyphs
                    .GroupBy(x => x.Y)
                    .OrderBy(x => x.Key)
                    .Select(row => string.Concat(row.OrderBy(x => x.X).Select(x => x.Character)).Trim())
                    .Where(x => x.Length > 0)
                    .ToList();
            }
        }
    }

    public LedColor GetPixel(int x, int y)
    {
        lock (_lock)
        {
            return _pixels[y, x];
        }
    }

    public int LitPixelCount
    {
        get
        {
            lock (_lock)
            {
                return _pixels.Cast<LedColor>().Count(x => x != LedColor.Off);
            }
        }
    }

    /// <summary>Applies one protocol line and returns the reply the board would send.</summary>
    public string Execute(string line)
    {
        var parts = line.Split(';', 4);
        var command = parts[0].Trim().ToUpperInvariant();
        lock (_lock)
        {
            switch (command)
            {
                case "CLEAR" when parts.Length == 1:
                    _glyphs.Clear();
                    Array.Clear(_pixels);
                    return "OK";

                case "COLOR" when parts.Length == 2 && TryParse(parts[1], 1, 7, out var color):
                    Color = (LedColor)color;
                    return "OK";

                case "FONT" when parts.Length == 2 && TryParse(parts[1], 0, 4, out var font):
                    Font = (LedFont)font;
                    return "OK";

                case "BRIGHT" when parts.Length == 2 && TryParse(parts[1], LedBrightness.Min, LedBrightness.Auto, out var level):
                    Brightness = level;
                    return "OK";

                case "SET" when parts.Length == 4
                                && TryParse(parts[1], 0, int.MaxValue, out var y)
                                && TryParse(parts[2], 0, int.MaxValue, out var x)
                                && TryUnquote(parts[3], out var text):
                    DrawText(x, y, text);
                    return "OK";

                case "PIX" when parts.Length == 4
                                && TryParse(parts[1], 0, Height - 1, out var pixelY)
                                && TryParse(parts[2], 0, Width - 1, out var pixelX)
                                && TryParse(parts[3], 0, 7, out var pixelColor):
                    _pixels[pixelY, pixelX] = (LedColor)pixelColor;
                    return "OK";

                default:
                    return "ERR";
            }
        }
    }

    /// <summary>
    /// ASCII view: pixels as colour letters (R G Y B M C W), text cells as '_' with the character in
    /// the middle of its cell, dark pixels as '.'.
    /// </summary>
    public string Render()
    {
        lock (_lock)
        {
            var canvas = new char[Height, Width];
            for (var row = 0; row < Height; row++)
            {
                for (var column = 0; column < Width; column++)
                {
                    canvas[row, column] = ColorLetter(_pixels[row, column]);
                }
            }

            foreach (var glyph in _glyphs)
            {
                var cellWidth = LedFontMetrics.CellWidth(glyph.Font);
                var cellHeight = LedFontMetrics.CellHeight(glyph.Font);
                for (var row = glyph.Y; row < glyph.Y + cellHeight; row++)
                {
                    for (var column = glyph.X; column < glyph.X + cellWidth; column++)
                    {
                        canvas[row, column] = '_';
                    }
                }

                canvas[glyph.Y + cellHeight / 2, glyph.X + cellWidth / 2] = glyph.Character == ' ' ? '_' : glyph.Character;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"+{new string('-', Width)}+  colour {Color}, brightness {(Brightness == LedBrightness.Auto ? "auto" : Brightness)}");
            for (var row = 0; row < Height; row++)
            {
                builder.Append('|');
                for (var column = 0; column < Width; column++)
                {
                    builder.Append(canvas[row, column]);
                }

                builder.AppendLine("|");
            }

            builder.AppendLine($"+{new string('-', Width)}+");
            foreach (var line in TextLinesUnlocked())
            {
                builder.AppendLine($"  \"{line}\"");
            }

            return builder.ToString();
        }
    }

    private IEnumerable<string> TextLinesUnlocked() =>
        _glyphs.GroupBy(x => x.Y).OrderBy(x => x.Key)
            .Select(row => string.Concat(row.OrderBy(x => x.X).Select(x => x.Character)).Trim())
            .Where(x => x.Length > 0);

    private void DrawText(int x, int y, string text)
    {
        var cellWidth = LedFontMetrics.CellWidth(Font);
        var cellHeight = LedFontMetrics.CellHeight(Font);
        for (var index = 0; index < text.Length; index++)
        {
            var left = x + index * cellWidth;
            if (left + cellWidth > Width || y + cellHeight > Height)
            {
                continue;
            }

            // A new glyph replaces whatever glyph occupied the same cell.
            _glyphs.RemoveAll(g => g.X == left && g.Y == y);
            _glyphs.Add(new EmulatedGlyph(left, y, text[index], Font));
        }
    }

    private static bool TryParse(string value, int min, int max, out int result) =>
        int.TryParse(value.Trim(), out result) && result >= min && result <= max;

    private static bool TryUnquote(string value, out string text)
    {
        text = string.Empty;
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return false;
        }

        text = value[1..^1];
        return true;
    }

    private static char ColorLetter(LedColor color) => color switch
    {
        LedColor.Red => 'R',
        LedColor.Green => 'G',
        LedColor.Yellow => 'Y',
        LedColor.Blue => 'B',
        LedColor.Magenta => 'M',
        LedColor.Cyan => 'C',
        LedColor.White => 'W',
        _ => '.',
    };
}
