using System.Text.RegularExpressions;
using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.Rendering;

/// <summary>
/// Ready-made full-screen frames for the typical sign contents. <c>fonts</c> are the fonts of the board
/// the frame is for (<c>null</c> - the Elitel fonts).
/// </summary>
public static class LedSigns
{
    /// <summary>A text centred on the whole screen in the biggest font it fits in, word-wrapped.</summary>
    public static LedFrame Message(int width, int height, string text, LedColor color, int? brightness = null,
        LedFontSet? fonts = null) =>
        new LedFrameBuilder(width, height, fonts)
            .WithColor(color)
            .WithBrightness(brightness)
            .TextBox(new LedRect(0, 0, width, height), text)
            .Build();

    /// <summary>
    /// A text on a single line, centred, in the biggest font the whole line fits in - for single-line
    /// boards. Line breaks become spaces; a line too long even for the smallest font is cut.
    /// </summary>
    public static LedFrame SingleLine(int width, int height, string text, LedColor color, int? brightness = null,
        LedFontSet? fonts = null) =>
        new LedFrameBuilder(width, height, fonts)
            .WithColor(color)
            .WithBrightness(brightness)
            .TextBox(new LedRect(0, 0, width, height), ToSingleLine(text), wrap: false)
            .Build();

    /// <summary>A number (e.g. free places) centred in the biggest font it fits in, never wrapped.</summary>
    public static LedFrame Number(int width, int height, int value, LedColor color, int? brightness = null,
        LedFontSet? fonts = null) =>
        new LedFrameBuilder(width, height, fonts)
            .WithColor(color)
            .WithBrightness(brightness)
            .TextBox(new LedRect(0, 0, width, height), value.ToString(), wrap: false)
            .Build();

    /// <summary>
    /// A symbol with an optional caption. On a wide board the symbol takes a square on the left and
    /// the caption the rest, on a tall one the symbol sits on top; on a square-ish board the symbol
    /// takes the upper 60% and the caption the band below.
    /// </summary>
    /// <param name="symbolColor">Colour of the symbol pixels.</param>
    /// <param name="captionColor">Colour of the caption - it is the frame colour, so it defaults to <paramref name="symbolColor"/>.</param>
    public static LedFrame Symbol(
        int width,
        int height,
        LedSymbol symbol,
        LedColor symbolColor,
        string? caption = null,
        LedColor? captionColor = null,
        LedColor accentColor = LedColor.Off,
        int? brightness = null,
        LedFontSet? fonts = null)
    {
        var builder = new LedFrameBuilder(width, height, fonts)
            .WithColor(captionColor ?? symbolColor)
            .WithBrightness(brightness);

        if (string.IsNullOrWhiteSpace(caption))
        {
            return builder.Symbol(builder.Bounds, symbol, symbolColor, accentColor).Build();
        }

        LedRect symbolBox;
        LedRect captionBox;
        if (width * 2 >= height * 3)
        {
            symbolBox = new LedRect(0, 0, height, height);
            captionBox = new LedRect(height + 1, 0, width - height - 1, height);
        }
        else if (height * 2 >= width * 3)
        {
            symbolBox = new LedRect(0, 0, width, width);
            captionBox = new LedRect(0, width + 1, width, height - width - 1);
        }
        else
        {
            var symbolHeight = height * 3 / 5;
            symbolBox = new LedRect(0, 0, width, symbolHeight);
            captionBox = new LedRect(0, symbolHeight + 1, width, height - symbolHeight - 1);
        }

        return builder
            .Symbol(symbolBox, symbol, symbolColor, accentColor)
            .TextBox(captionBox, caption)
            .Build();
    }

    private static string ToSingleLine(string? text) =>
        Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim();
}
