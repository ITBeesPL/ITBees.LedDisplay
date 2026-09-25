using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.Rendering;

/// <summary>
/// Complete description of what a board should show. A frame is always drawn from a blank screen,
/// so two frames with the same <see cref="Signature"/> look the same and resending one is pointless.
/// </summary>
/// <remarks>
/// The controller keeps a single current colour for all texts (<c>COLOR</c> recolours what is already
/// on the screen), hence one <see cref="Color"/> per frame. Pixels carry their own colour.
/// </remarks>
public sealed class LedFrame
{
    public LedFrame(
        int width,
        int height,
        LedColor color,
        int? brightness,
        IReadOnlyList<LedText> texts,
        IReadOnlyList<LedPixel> pixels,
        string? description = null)
    {
        if (color is < LedColor.Red or > LedColor.White)
        {
            throw new ArgumentOutOfRangeException(nameof(color), color, "Frame colour must be 1..7");
        }

        if (brightness.HasValue && LedBrightness.IsValid(brightness.Value) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(brightness), brightness, "Brightness must be 1..10");
        }

        Width = width;
        Height = height;
        Color = color;
        Brightness = brightness;
        Texts = texts;
        Pixels = pixels;
        Description = description ?? string.Join(" | ", texts.Select(x => x.Text.Trim()).Where(x => x.Length > 0));
        Signature = string.Join("\n", ElitelFrameCompiler.Compile(this).Select(x => x.Line));
    }

    public int Width { get; }
    public int Height { get; }
    public LedColor Color { get; }

    /// <summary>Brightness sent with the frame; <c>null</c> leaves the board's current setting alone.</summary>
    public int? Brightness { get; }

    public IReadOnlyList<LedText> Texts { get; }
    public IReadOnlyList<LedPixel> Pixels { get; }

    /// <summary>Human-readable summary (texts and symbol names) for logs and status screens.</summary>
    public string Description { get; }

    /// <summary>The compiled command sequence - equal signatures mean identical screens.</summary>
    public string Signature { get; }

    public static LedFrame Blank(int width, int height, int? brightness = null) =>
        new(width, height, LedColor.White, brightness, Array.Empty<LedText>(), Array.Empty<LedPixel>(), string.Empty);

    public override string ToString() => Description;
}
