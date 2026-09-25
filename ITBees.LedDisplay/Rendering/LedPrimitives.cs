using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.Rendering;

/// <summary>Rectangle in display pixels, origin at the top-left corner.</summary>
public readonly record struct LedRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool FitsIn(int displayWidth, int displayHeight) =>
        X >= 0 && Y >= 0 && Width >= 0 && Height >= 0 && Right <= displayWidth && Bottom <= displayHeight;
}

/// <summary>A single line of text drawn at a pixel position with one of the built-in fonts.</summary>
public readonly record struct LedText(int X, int Y, string Text, LedFont Font);

/// <summary>A single pixel. Pixels are drawn after the texts.</summary>
public readonly record struct LedPixel(int X, int Y, LedColor Color);

public enum LedHorizontalAlignment
{
    Left,
    Center,
    Right,
}

public enum LedVerticalAlignment
{
    Top,
    Middle,
    Bottom,
}
