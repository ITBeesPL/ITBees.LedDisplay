using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.Rendering;

/// <summary>Fluent builder of a <see cref="LedFrame"/> for a board of a given size.</summary>
public sealed class LedFrameBuilder
{
    private readonly List<LedText> _texts = new();
    private readonly List<LedPixel> _pixels = new();
    private readonly List<string> _descriptions = new();
    private LedColor _color = LedColor.White;
    private int? _brightness;

    public LedFrameBuilder(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), $"Display size must be positive, got {width}x{height}");
        }

        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }
    public LedRect Bounds => new(0, 0, Width, Height);

    /// <summary>Colour of all texts in the frame (the controller has one current colour for the whole screen).</summary>
    public LedFrameBuilder WithColor(LedColor color)
    {
        if (color is < LedColor.Red or > LedColor.White)
        {
            throw new ArgumentOutOfRangeException(nameof(color), color, "Frame colour must be 1..7");
        }

        _color = color;
        return this;
    }

    /// <summary>1..9 fixed, <see cref="LedBrightness.Auto"/> automatic, <c>null</c> keeps the board's setting.</summary>
    public LedFrameBuilder WithBrightness(int? level)
    {
        if (level.HasValue && LedBrightness.IsValid(level.Value) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Brightness must be 1..10");
        }

        _brightness = level;
        return this;
    }

    /// <summary>
    /// A line of text at an exact position. Throws when the text would not fit - the controller
    /// would silently drop the characters that stick out.
    /// </summary>
    public LedFrameBuilder Text(int x, int y, string text, LedFont font)
    {
        var sanitized = ElitelTextEncoding.Sanitize(text);
        var area = new LedRect(x, y, LedFontMetrics.MeasureWidth(sanitized, font), LedFontMetrics.CellHeight(font));
        if (area.FitsIn(Width, Height) == false)
        {
            throw new ArgumentException(
                $"Text '{sanitized}' in {font} at ({x},{y}) needs {area.Width}x{area.Height} px and does not fit a {Width}x{Height} display");
        }

        if (sanitized.Length > 0)
        {
            _texts.Add(new LedText(x, y, sanitized, font));
            _descriptions.Add(sanitized.Trim());
        }

        return this;
    }

    /// <summary>
    /// Text fitted into a box: the biggest font up to <paramref name="maxFont"/> in which the whole
    /// text fits (word-wrapped when <paramref name="wrap"/>), aligned inside the box. When even the
    /// smallest font is too big the text is cut. The box is clipped to the display.
    /// </summary>
    public LedFrameBuilder TextBox(
        LedRect box,
        string? text,
        LedFont maxFont = LedFont.Font12x24,
        LedHorizontalAlignment horizontal = LedHorizontalAlignment.Center,
        LedVerticalAlignment vertical = LedVerticalAlignment.Middle,
        bool wrap = true,
        bool shrinkToFit = true)
    {
        // Line breaks are layout instructions here, so they survive sanitising (which blanks control characters).
        var sanitized = string.Join("\n", (text ?? string.Empty).Replace("\r\n", "\n").Split('\n')
            .Select(ElitelTextEncoding.Sanitize));
        var clipped = Clip(box);
        if (string.IsNullOrWhiteSpace(sanitized) || clipped.Width == 0 || clipped.Height == 0)
        {
            return this;
        }

        var fit = LedTextLayout.Fit(sanitized, clipped, maxFont, wrap, shrinkToFit);
        if (fit == null || fit.Lines.Count == 0)
        {
            return this;
        }

        var cellWidth = LedFontMetrics.CellWidth(fit.Font);
        var cellHeight = LedFontMetrics.CellHeight(fit.Font);
        var blockHeight = fit.Lines.Count * cellHeight;
        var top = vertical switch
        {
            LedVerticalAlignment.Top => clipped.Y,
            LedVerticalAlignment.Bottom => clipped.Bottom - blockHeight,
            _ => clipped.Y + (clipped.Height - blockHeight) / 2,
        };

        for (var index = 0; index < fit.Lines.Count; index++)
        {
            var line = fit.Lines[index];
            if (line.Length == 0)
            {
                continue;
            }

            var lineWidth = line.Length * cellWidth;
            var left = horizontal switch
            {
                LedHorizontalAlignment.Left => clipped.X,
                LedHorizontalAlignment.Right => clipped.Right - lineWidth,
                _ => clipped.X + (clipped.Width - lineWidth) / 2,
            };

            _texts.Add(new LedText(left, top + index * cellHeight, line, fit.Font));
        }

        _descriptions.Add(string.Join(" ", fit.Lines.Where(x => x.Length > 0)));
        return this;
    }

    /// <summary>A symbol scaled to the largest square that fits the box, centred in it.</summary>
    public LedFrameBuilder Symbol(LedRect box, LedSymbol symbol, LedColor color, LedColor accentColor = LedColor.Off)
    {
        var pixels = LedSymbolRasterizer.Rasterize(symbol, Clip(box), color, accentColor);
        if (pixels.Count > 0)
        {
            _pixels.AddRange(pixels);
            _descriptions.Add($"[{symbol}]");
        }

        return this;
    }

    public LedFrameBuilder Pixel(int x, int y, LedColor color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel ({x},{y}) is outside a {Width}x{Height} display");
        }

        _pixels.Add(new LedPixel(x, y, color));
        return this;
    }

    public LedFrame Build() =>
        new(Width, Height, _color, _brightness, _texts.ToList(), _pixels.ToList(), string.Join(" | ", _descriptions));

    private LedRect Clip(LedRect box)
    {
        var left = Math.Clamp(box.X, 0, Width);
        var top = Math.Clamp(box.Y, 0, Height);
        var right = Math.Clamp(box.Right, left, Width);
        var bottom = Math.Clamp(box.Bottom, top, Height);
        return new LedRect(left, top, right - left, bottom - top);
    }
}
