using ITBees.LedDisplay.Protocol;
using ITBees.LedDisplay.Rendering;

namespace ITBees.LedDisplay.Layouts;

/// <summary>
/// Configurable board layout: named rectangular zones, each showing a value supplied at render time
/// (or its static text). Serialisable, so a layout can be kept as device configuration - e.g. a
/// free-places board made of several segments: level label, general, disabled and EV counters.
/// </summary>
public class LedLayout
{
    public int Width { get; set; } = 64;
    public int Height { get; set; } = 32;

    /// <summary>Colour of all texts (the controller has one current colour per screen).</summary>
    public LedColor Color { get; set; } = LedColor.Green;

    public int? Brightness { get; set; }
    public List<LedZone> Zones { get; set; } = new();
}

public class LedZone
{
    /// <summary>Name the value for this zone is looked up by. Zones without a key show <see cref="Text"/> only.</summary>
    public string? Key { get; set; }

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Biggest font to use; smaller ones are tried when the value does not fit.</summary>
    public LedFont MaxFont { get; set; } = LedFont.Font8x16;

    public LedHorizontalAlignment HorizontalAlignment { get; set; } = LedHorizontalAlignment.Center;
    public LedVerticalAlignment VerticalAlignment { get; set; } = LedVerticalAlignment.Middle;
    public bool Wrap { get; set; }

    /// <summary>Static text (a label) shown when no value is supplied for <see cref="Key"/>.</summary>
    public string? Text { get; set; }

    public LedRect Box => new(X, Y, Width, Height);
}

public static class LedLayoutRenderer
{
    /// <summary>
    /// Draws every zone with the value found under its key (case-insensitive) or its static text.
    /// Values that do not fit are shrunk and, as a last resort, cut - see <see cref="LedTextLayout.Fit"/>.
    /// </summary>
    public static LedFrame Render(LedLayout layout, IReadOnlyDictionary<string, string?>? values = null)
    {
        var lookup = values == null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);

        var builder = new LedFrameBuilder(layout.Width, layout.Height)
            .WithColor(layout.Color)
            .WithBrightness(layout.Brightness);

        foreach (var zone in layout.Zones)
        {
            var text = zone.Key != null && lookup.TryGetValue(zone.Key, out var value) ? value : zone.Text;
            builder.TextBox(zone.Box, text, zone.MaxFont, zone.HorizontalAlignment, zone.VerticalAlignment, zone.Wrap);
        }

        return builder.Build();
    }
}
