using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.Rendering;

/// <summary>Result of fitting a text into a box: the chosen font and the lines to draw.</summary>
public sealed record LedTextFit(LedFont Font, IReadOnlyList<string> Lines, bool Truncated);

/// <summary>
/// Fits text into a rectangle using the built-in fixed-width fonts. The controller silently drops
/// a character that does not fit on the screen as a whole, so text is fitted here instead of
/// being sent and partially lost.
/// </summary>
public static class LedTextLayout
{
    /// <summary>
    /// Tries <paramref name="maxFont"/> and - with <paramref name="shrinkToFit"/> - every smaller font,
    /// returning the biggest one the whole text fits in. A font that keeps words whole wins over a
    /// bigger one that would have to split a word across lines. If nothing fits, the smallest usable
    /// font is taken and the text is cut to the box (<see cref="LedTextFit.Truncated"/>). Line breaks
    /// (<c>\n</c>) in the text are always honoured; <paramref name="wrap"/> adds word wrapping.
    /// Returns <c>null</c> when the box is smaller than a single character.
    /// </summary>
    public static LedTextFit? Fit(string text, LedRect box, LedFont maxFont, bool wrap, bool shrinkToFit)
    {
        var candidates = (shrinkToFit
                ? LedFontMetrics.LargestFirst.SkipWhile(x => x != maxFont)
                : new[] { maxFont })
            .Select(font => (Font: font,
                Columns: box.Width / LedFontMetrics.CellWidth(font),
                Rows: box.Height / LedFontMetrics.CellHeight(font)))
            .Where(x => x.Columns >= 1 && x.Rows >= 1)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (var allowWordBreaks in new[] { false, true })
        {
            foreach (var (font, columns, rows) in candidates)
            {
                var lines = SplitLines(text, columns, wrap, out var brokeWords);
                if ((allowWordBreaks || brokeWords == false) && lines.Count <= rows && lines.All(x => x.Length <= columns))
                {
                    return new LedTextFit(font, lines, false);
                }
            }
        }

        var fallback = candidates[^1];
        var truncated = SplitLines(text, fallback.Columns, wrap, out _)
            .Take(fallback.Rows)
            .Select(x => x.Length > fallback.Columns ? x[..fallback.Columns] : x)
            .ToList();
        return new LedTextFit(fallback.Font, truncated, true);
    }

    /// <summary>
    /// Greedy word wrap to <paramref name="maxColumns"/> characters per line; words longer than a
    /// line are split. Explicit <c>\n</c> always starts a new line.
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, int maxColumns) => SplitLines(text, maxColumns, true, out _);

    private static List<string> SplitLines(string text, int maxColumns, bool wrap, out bool brokeWords)
    {
        brokeWords = false;
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = paragraph.Trim();
            if (wrap == false)
            {
                lines.Add(trimmed);
                continue;
            }

            var line = string.Empty;
            foreach (var word in trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var rest = word;
                while (rest.Length > maxColumns)
                {
                    if (line.Length > 0)
                    {
                        lines.Add(line);
                        line = string.Empty;
                    }

                    lines.Add(rest[..maxColumns]);
                    rest = rest[maxColumns..];
                    brokeWords = true;
                }

                if (rest.Length == 0)
                {
                    continue;
                }

                if (line.Length == 0)
                {
                    line = rest;
                }
                else if (line.Length + 1 + rest.Length <= maxColumns)
                {
                    line += " " + rest;
                }
                else
                {
                    lines.Add(line);
                    line = rest;
                }
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        // Blank lines only matter between texts - leading/trailing ones would just push the block off centre.
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        while (lines.Count > 0 && lines[0].Length == 0)
        {
            lines.RemoveAt(0);
        }

        return lines;
    }
}
