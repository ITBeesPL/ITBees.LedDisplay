namespace ITBees.LedDisplay.Protocol;

/// <summary>
/// Colour as understood by the Elitel controller: a 3-bit RGB mask (1 = red, 2 = green, 4 = blue),
/// components add up to the mixed colours.
/// </summary>
[Flags]
public enum LedColor
{
    /// <summary>Unlit. Valid only for single pixels - <c>COLOR</c> accepts 1..7.</summary>
    Off = 0,
    Red = 1,
    Green = 2,
    Yellow = Red | Green,
    Blue = 4,
    Magenta = Red | Blue,
    Cyan = Green | Blue,
    White = Red | Green | Blue,
}
