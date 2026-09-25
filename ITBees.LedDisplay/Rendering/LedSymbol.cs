using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.Rendering;

/// <summary>Traffic symbols drawn pixel by pixel - the controller has no built-in graphics.</summary>
public enum LedSymbol
{
    ArrowUp,
    ArrowDown,
    ArrowLeft,
    ArrowRight,

    /// <summary>Diagonal cross - "lane closed" over a traffic lane.</summary>
    Cross,

    /// <summary>
    /// B-2 "no entry": a disc with a horizontal bar. The bar gets the accent colour, unlit by default,
    /// which keeps the sign readable even if a board turns out to ignore per-pixel colours.
    /// </summary>
    NoEntry,
}

/// <summary>
/// Rasterises <see cref="LedSymbol"/> into pixels. Shapes are defined on a square centred at the origin
/// (coordinates -0.5..0.5) and sampled at pixel centres, so a symbol scales to any square box (it is
/// centred in non-square ones) and mirror-symmetric shapes stay pixel-exact symmetric.
/// </summary>
public static class LedSymbolRasterizer
{
    /// <summary>Below this edge length none of the shapes is recognisable.</summary>
    public const int MinimumSize = 5;

    private const double Margin = 0.06;
    private const double Edge = 0.5 - Margin;

    private enum Paint
    {
        None,
        Main,
        Accent,
    }

    public static IReadOnlyList<LedPixel> Rasterize(
        LedSymbol symbol,
        LedRect box,
        LedColor color,
        LedColor accentColor = LedColor.Off)
    {
        var size = Math.Min(box.Width, box.Height);
        if (size < MinimumSize)
        {
            return Array.Empty<LedPixel>();
        }

        var originX = box.X + (box.Width - size) / 2;
        var originY = box.Y + (box.Height - size) / 2;
        var pixels = new List<LedPixel>();

        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                // Integer numerators keep mirrored pixels exact negatives of each other.
                var x = (2 * column + 1 - size) / (2.0 * size);
                var y = (2 * row + 1 - size) / (2.0 * size);
                var pixelColor = Sample(symbol, x, y, size) switch
                {
                    Paint.Main => color,
                    Paint.Accent => accentColor,
                    _ => LedColor.Off,
                };

                if (pixelColor != LedColor.Off)
                {
                    pixels.Add(new LedPixel(originX + column, originY + row, pixelColor));
                }
            }
        }

        return pixels;
    }

    private static Paint Sample(LedSymbol symbol, double x, double y, int size) => symbol switch
    {
        // All arrows are the downward arrow seen through a mirror or a transposition.
        LedSymbol.ArrowDown => ArrowDown(x, y),
        LedSymbol.ArrowUp => ArrowDown(x, -y),
        LedSymbol.ArrowRight => ArrowDown(y, x),
        LedSymbol.ArrowLeft => ArrowDown(y, -x),
        LedSymbol.Cross => Cross(x, y, size),
        LedSymbol.NoEntry => NoEntry(x, y),
        _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown symbol"),
    };

    private static Paint ArrowDown(double x, double y)
    {
        const double shaftHalfWidth = 0.12;
        const double shaftEnd = 0.0;
        const double headBase = -0.08;

        var offset = Math.Abs(x);
        var inShaft = offset <= shaftHalfWidth && y >= -Edge && y <= shaftEnd;
        var inHead = y >= headBase && y <= Edge && offset <= Edge * (Edge - y) / (Edge - headBase);
        return inShaft || inHead ? Paint.Main : Paint.None;
    }

    private static Paint Cross(double x, double y, int size)
    {
        if (Math.Abs(x) > Edge || Math.Abs(y) > Edge)
        {
            return Paint.None;
        }

        // Stroke of ~16% of the size, but never under 1.6 px or the diagonals break up on small boards.
        var thickness = Math.Max(0.16, 1.6 / size);
        var limit = thickness / Math.Sqrt(2);
        return Math.Abs(x - y) <= limit || Math.Abs(x + y) <= limit ? Paint.Main : Paint.None;
    }

    private static Paint NoEntry(double x, double y)
    {
        const double radius = 0.5 - Margin / 2;
        const double barHalfHeight = 0.09;
        const double barHalfLength = 0.34;

        if (x * x + y * y > radius * radius)
        {
            return Paint.None;
        }

        return Math.Abs(y) <= barHalfHeight && Math.Abs(x) <= barHalfLength ? Paint.Accent : Paint.Main;
    }
}
