using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.Rendering;

/// <summary>Turns a <see cref="LedFrame"/> into the Elitel command sequence that draws it.</summary>
public static class ElitelFrameCompiler
{
    /// <summary>
    /// Order: brightness first (so the new content never flashes at the old level), then a clear
    /// screen, the colour, the texts (a <c>FONT</c> only when it changes) and finally the pixels.
    /// Unlit pixels are skipped - the screen has just been cleared.
    /// </summary>
    public static IReadOnlyList<LedCommand> Compile(LedFrame frame)
    {
        var commands = new List<LedCommand>(3 + frame.Texts.Count * 2 + frame.Pixels.Count);

        if (frame.Brightness.HasValue)
        {
            commands.Add(ElitelCommands.Brightness(frame.Brightness.Value));
        }

        commands.Add(ElitelCommands.Clear());
        commands.Add(ElitelCommands.Color(frame.Color));

        LedFont? currentFont = null;
        foreach (var text in frame.Texts)
        {
            if (currentFont != text.Font)
            {
                commands.Add(ElitelCommands.Font(text.Font));
                currentFont = text.Font;
            }

            commands.Add(ElitelCommands.SetText(text.X, text.Y, text.Text));
        }

        foreach (var pixel in frame.Pixels)
        {
            if (pixel.Color != LedColor.Off)
            {
                commands.Add(ElitelCommands.Pixel(pixel.X, pixel.Y, pixel.Color));
            }
        }

        return commands;
    }
}
