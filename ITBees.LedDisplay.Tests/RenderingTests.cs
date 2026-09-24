using ITBees.LedDisplay.Layouts;
using ITBees.LedDisplay.Protocol;
using ITBees.LedDisplay.Rendering;
using Xunit;

namespace ITBees.LedDisplay.Tests;

public class RenderingTests
{
    [Fact]
    public void Fit_PicksTheBiggestFontTheTextFitsIn()
    {
        // 64x32: "123" is 36 px wide in 12x24.
        var fit = LedTextLayout.Fit("123", new LedRect(0, 0, 64, 32), LedFont.Font12x24, wrap: false, shrinkToFit: true);

        Assert.NotNull(fit);
        Assert.Equal(LedFont.Font12x24, fit.Font);
        Assert.Equal(new[] { "123" }, fit.Lines);
        Assert.False(fit.Truncated);
    }

    [Fact]
    public void Fit_ShrinksAndWrapsALongMessage()
    {
        // 16 characters per line in 4x6 would be needed without wrapping; 8x8 gives 8 columns x 4 rows.
        var fit = LedTextLayout.Fit("PARKING ZAMKNIĘTY", new LedRect(0, 0, 64, 32), LedFont.Font12x24, wrap: true, shrinkToFit: true);

        Assert.NotNull(fit);
        Assert.Equal(LedFont.Font6x8, fit.Font);
        Assert.Equal(new[] { "PARKING", "ZAMKNIĘTY" }, fit.Lines);
    }

    [Fact]
    public void Fit_TruncatesWhenEvenTheSmallestFontIsTooBig()
    {
        var fit = LedTextLayout.Fit("ABCDEFGHIJ", new LedRect(0, 0, 16, 6), LedFont.Font8x16, wrap: false, shrinkToFit: true);

        Assert.NotNull(fit);
        Assert.Equal(LedFont.Font4x6, fit.Font);
        Assert.Equal(new[] { "ABCD" }, fit.Lines);
        Assert.True(fit.Truncated);
    }

    [Fact]
    public void Fit_ReturnsNullForABoxSmallerThanACharacter()
    {
        Assert.Null(LedTextLayout.Fit("A", new LedRect(0, 0, 3, 5), LedFont.Font4x6, wrap: false, shrinkToFit: true));
    }

    [Fact]
    public void Wrap_SplitsWordsLongerThanALine()
    {
        Assert.Equal(new[] { "ABCD", "EFGH", "IJ", "KL" }, LedTextLayout.Wrap("ABCDEFGHIJ KL", 4));
    }

    [Fact]
    public void Wrap_HonoursExplicitLineBreaks()
    {
        Assert.Equal(new[] { "TYLKO", "WYJAZD" }, LedTextLayout.Wrap("TYLKO\nWYJAZD", 20));
    }

    [Fact]
    public void TextBox_AlignsInsideTheBox()
    {
        var frame = new LedFrameBuilder(64, 32)
            .TextBox(new LedRect(0, 0, 64, 32), "12", LedFont.Font8x16, LedHorizontalAlignment.Right, LedVerticalAlignment.Bottom, wrap: false, shrinkToFit: false)
            .Build();

        var text = Assert.Single(frame.Texts);
        Assert.Equal(new LedText(48, 16, "12", LedFont.Font8x16), text);
    }

    [Fact]
    public void TextBox_CentresEveryLineOfAWrappedText()
    {
        var frame = new LedFrameBuilder(64, 32).TextBox(new LedRect(0, 0, 64, 32), "PARKING ZAMKNIĘTY").Build();

        Assert.Equal(2, frame.Texts.Count);
        Assert.Equal(new LedText(11, 8, "PARKING", LedFont.Font6x8), frame.Texts[0]);
        Assert.Equal(new LedText(5, 16, "ZAMKNIĘTY", LedFont.Font6x8), frame.Texts[1]);
        Assert.Equal("PARKING ZAMKNIĘTY", frame.Description);
    }

    [Fact]
    public void Text_RefusesTextThatWouldBeCutByTheBoard()
    {
        var builder = new LedFrameBuilder(64, 32);

        Assert.Throws<ArgumentException>(() => builder.Text(40, 0, "ABC", LedFont.Font12x24));
    }

    [Fact]
    public void Compile_ClearsSetsColourAndChangesFontOnlyWhenNeeded()
    {
        var frame = new LedFrameBuilder(64, 32)
            .WithColor(LedColor.Green)
            .WithBrightness(LedBrightness.Auto)
            .Text(0, 0, "A", LedFont.Font8x8)
            .Text(8, 0, "B", LedFont.Font8x8)
            .Text(0, 8, "C", LedFont.Font4x6)
            .Pixel(63, 31, LedColor.Red)
            .Pixel(0, 31, LedColor.Off)
            .Build();

        var lines = ElitelFrameCompiler.Compile(frame).Select(x => x.Line);

        Assert.Equal(new[]
        {
            "BRIGHT;10", "CLEAR", "COLOR;2",
            "FONT;2", "SET;0;0;\"A\"", "SET;0;8;\"B\"",
            "FONT;0", "SET;8;0;\"C\"",
            "PIX;31;63;1",
        }, lines);
    }

    [Fact]
    public void Signature_IsEqualForIdenticalFrames()
    {
        var first = LedSigns.Number(64, 32, 42, LedColor.Green);
        var second = LedSigns.Number(64, 32, 42, LedColor.Green);
        var other = LedSigns.Number(64, 32, 43, LedColor.Green);

        Assert.Equal(first.Signature, second.Signature);
        Assert.NotEqual(first.Signature, other.Signature);
    }

    [Theory]
    [InlineData(LedSymbol.ArrowDown)]
    [InlineData(LedSymbol.ArrowUp)]
    [InlineData(LedSymbol.ArrowLeft)]
    [InlineData(LedSymbol.ArrowRight)]
    [InlineData(LedSymbol.Cross)]
    [InlineData(LedSymbol.NoEntry)]
    public void Symbols_StayInsideTheirBoxAndAreSymmetric(LedSymbol symbol)
    {
        var box = new LedRect(10, 4, 40, 24);
        var pixels = LedSymbolRasterizer.Rasterize(symbol, box, LedColor.Green);

        Assert.NotEmpty(pixels);
        Assert.All(pixels, p => Assert.True(p.X >= 18 && p.X < 42 && p.Y >= 4 && p.Y < 28, $"{p} outside the 24x24 square"));

        var set = pixels.Select(p => (p.X - 18, p.Y - 4)).ToHashSet();
        var mirrorAxisIsVertical = symbol is not (LedSymbol.ArrowLeft or LedSymbol.ArrowRight);
        foreach (var (x, y) in set)
        {
            var mirrored = mirrorAxisIsVertical ? (23 - x, y) : (x, 23 - y);
            Assert.Contains(mirrored, set);
        }
    }

    [Fact]
    public void ArrowDown_IsWiderAtTheTopOfTheHeadThanAtTheShaft()
    {
        var pixels = LedSymbolRasterizer.Rasterize(LedSymbol.ArrowDown, new LedRect(0, 0, 32, 32), LedColor.Green);
        int WidthOfRow(int row) => pixels.Count(p => p.Y == row);

        Assert.True(WidthOfRow(15) > 2 * WidthOfRow(5));
        Assert.True(WidthOfRow(28) < WidthOfRow(15));
    }

    [Fact]
    public void NoEntry_LeavesTheBarUnlitByDefaultAndPaintsItWithAnAccent()
    {
        var box = new LedRect(0, 0, 32, 32);
        var plain = LedSymbolRasterizer.Rasterize(LedSymbol.NoEntry, box, LedColor.Red);
        var accented = LedSymbolRasterizer.Rasterize(LedSymbol.NoEntry, box, LedColor.Red, LedColor.White);

        Assert.DoesNotContain(plain, p => p.X == 16 && p.Y == 16);
        Assert.Contains(plain, p => p.X == 16 && p.Y == 4);
        Assert.Contains(accented, p => p is { X: 16, Y: 16, Color: LedColor.White });
    }

    [Fact]
    public void SymbolSign_PutsTheCaptionNextToTheSymbolOnAWideBoard()
    {
        var frame = LedSigns.Symbol(96, 32, LedSymbol.NoEntry, LedColor.Red, "TYLKO REZERWACJE", LedColor.White);

        Assert.All(frame.Pixels, p => Assert.True(p.X < 32));
        Assert.All(frame.Texts, t => Assert.True(t.X > 32));
        Assert.Equal(LedColor.White, frame.Color);
        Assert.Equal("[NoEntry] | TYLKO REZERWACJE", frame.Description);
    }

    [Fact]
    public void Layout_RendersValuesAndStaticLabels()
    {
        var layout = new LedLayout
        {
            Width = 96,
            Height = 32,
            Color = LedColor.Green,
            Zones =
            {
                new LedZone { X = 0, Y = 0, Width = 32, Height = 16, Text = "-2", MaxFont = LedFont.Font8x16 },
                new LedZone { Key = "free", X = 32, Y = 0, Width = 64, Height = 16, HorizontalAlignment = LedHorizontalAlignment.Right },
                new LedZone { Key = "missing", X = 0, Y = 16, Width = 96, Height = 16 },
            },
        };

        var frame = LedLayoutRenderer.Render(layout, new Dictionary<string, string?> { ["FREE"] = "137" });

        Assert.Equal(new LedText(8, 0, "-2", LedFont.Font8x16), frame.Texts[0]);
        Assert.Equal(new LedText(72, 0, "137", LedFont.Font8x16), frame.Texts[1]);
        Assert.Equal(2, frame.Texts.Count);
    }
}
