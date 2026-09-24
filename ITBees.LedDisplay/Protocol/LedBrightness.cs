namespace ITBees.LedDisplay.Protocol;

/// <summary>Values accepted by <c>BRIGHT;n</c>: 1..9 is a fixed level, 10 hands control to the light sensor.</summary>
public static class LedBrightness
{
    public const int Min = 1;
    public const int Max = 9;
    public const int Auto = 10;

    public static bool IsValid(int level) => level is >= Min and <= Auto;
}
