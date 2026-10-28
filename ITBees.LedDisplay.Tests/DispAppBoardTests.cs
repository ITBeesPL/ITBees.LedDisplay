using ITBees.LedDisplay.DeviceEmulator;
using ITBees.LedDisplay.Models;
using ITBees.LedDisplay.Protocol;
using ITBees.LedDisplay.Rendering;
using ITBees.LedDisplay.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ITBees.LedDisplay.Tests;

/// <summary>
/// Boards behind the DispApp RS485 converter: extra fonts (FONT;5..9), single-line texts, ASCII-only
/// glyphs and the RESET OK notice after a power cut.
/// </summary>
public class DispAppBoardTests
{
    private const string BigDigits = "6:16x32";

    [Fact]
    public void ParseSizes_ReadsFontNumberAndCell()
    {
        var sizes = LedFontSet.ParseSizes(" 5:10x20; 6:16X32 ,");

        Assert.Equal(new[] { new LedFontSize(LedFont.Font5, 10, 20), new LedFontSize(LedFont.Font6, 16, 32) }, sizes);
        Assert.Empty(LedFontSet.ParseSizes("  "));
    }

    [Theory]
    [InlineData("5:16")]
    [InlineData("10:8x8")]
    [InlineData("5:0x8")]
    [InlineData("big")]
    public void ParseSizes_RejectsMalformedEntries(string text)
    {
        Assert.Throws<FormatException>(() => LedFontSet.ParseSizes(text));
    }

    [Fact]
    public void FontSet_TriesExtraFontsBySize()
    {
        var fonts = LedFontSet.Elitel.With(LedFontSet.ParseSizes("5:10x20, " + BigDigits));

        Assert.Equal(
            new[] { LedFont.Font6, LedFont.Font12x24, LedFont.Font5, LedFont.Font8x16, LedFont.Font8x8, LedFont.Font6x8, LedFont.Font4x6 },
            fonts.LargestFirst);
        Assert.False(LedFontSet.Elitel.Contains(LedFont.Font6));
    }

    [Fact]
    public void Number_UsesTheBiggestFontOfTheBoard()
    {
        var fonts = LedFontSet.Elitel.With(LedFontSet.ParseSizes(BigDigits));

        var frame = LedSigns.Number(96, 32, 123, LedColor.Green, fonts: fonts);

        var text = Assert.Single(frame.Texts);
        Assert.Equal(LedFont.Font6, text.Font);
        Assert.Equal(new LedText(24, 0, "123", LedFont.Font6), text);
    }

    [Fact]
    public void SingleLine_KeepsTheTextOnOneLine()
    {
        var wrapped = LedSigns.Message(96, 32, "PARKING ZAMKNIĘTY", LedColor.Red);
        var single = LedSigns.SingleLine(96, 32, "PARKING\nZAMKNIĘTY", LedColor.Red);

        Assert.Equal(2, wrapped.Texts.Count);
        var line = Assert.Single(single.Texts);
        Assert.Equal("PARKING ZAMKNIĘTY", line.Text);
        Assert.Equal(LedFont.Font4x6, line.Font);
    }

    [Fact]
    public void ToAscii_DropsPolishDiacritics()
    {
        Assert.Equal("ZAMKNIETY Lodz zolc", ElitelTextEncoding.ToAscii("ZAMKNIĘTY Łódź żółć"));
        Assert.Equal("cena 5?", ElitelTextEncoding.ToAscii("cena 5€"));

        var bytes = ElitelCommands.SetText(0, 0, "WJAZD ZAMKNIĘTY").ToBytes(LedTextCharset.Ascii);
        Assert.All(bytes, x => Assert.True(x < 0x80));
        Assert.Equal((byte)'\n', bytes[^1]);
    }

    [Fact]
    public void Validate_RejectsBrokenExtraFonts()
    {
        var definition = ElitelLedDisplayClientTests.Definition(4001) with { ExtraFonts = "6:16" };

        Assert.Throws<ArgumentException>(definition.Validate);
    }

    [Fact]
    public async Task ShowAsync_SendsAsciiTextToAnAsciiBoard()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 96, 32).Start();
        var definition = ElitelLedDisplayClientTests.Definition(emulator.Port, 96, 32) with { Charset = LedTextCharset.Ascii };
        var client = new ElitelLedDisplayClient(definition, ElitelLedDisplayClientTests.FastOptions(), NullLogger.Instance);

        await client.ShowAsync(LedSigns.SingleLine(96, 32, "ZAMKNIĘTY", LedColor.Red));

        Assert.Contains(emulator.ReceivedLines, x => x.EndsWith("\"ZAMKNIETY\"", StringComparison.Ordinal));
        Assert.Equal(new[] { "ZAMKNIETY" }, emulator.Screen.TextLines);
    }

    [Fact]
    public async Task ShowAsync_DrawsWithTheExtraFontsOfTheBoard()
    {
        var fonts = LedFontSet.Elitel.With(LedFontSet.ParseSizes(BigDigits));
        await using var emulator = new ElitelDisplayEmulator(0, 96, 32, fonts).Start();
        var definition = ElitelLedDisplayClientTests.Definition(emulator.Port, 96, 32) with { ExtraFonts = BigDigits };
        var client = new ElitelLedDisplayClient(definition, ElitelLedDisplayClientTests.FastOptions(), NullLogger.Instance);

        await client.ShowAsync(LedSigns.Number(96, 32, 123, LedColor.Green, fonts: definition.GetFontSet()));

        Assert.Contains("FONT;6", emulator.ReceivedLines);
        Assert.Equal(new[] { "123" }, emulator.Screen.TextLines);
        Assert.All(emulator.Screen.Glyphs, x => Assert.Equal(LedFont.Font6, x.Font));
    }

    [Fact]
    public async Task ShowAsync_RefusesAFontTheBoardDoesNotHave()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 96, 32).Start();
        var client = new ElitelLedDisplayClient(
            ElitelLedDisplayClientTests.Definition(emulator.Port, 96, 32), ElitelLedDisplayClientTests.FastOptions(), NullLogger.Instance);
        var frame = LedSigns.Number(96, 32, 7, LedColor.Green, fonts: LedFontSet.Elitel.With(LedFontSet.ParseSizes(BigDigits)));

        await Assert.ThrowsAsync<ArgumentException>(() => client.ShowAsync(frame));
        Assert.Empty(emulator.ReceivedLines);
    }

    [Fact]
    public async Task ShowAsync_FailsWhenTheBoardReportsARestart_AndWorksOnTheNextAttempt()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 64, 32).Start();
        var client = new ElitelLedDisplayClient(
            ElitelLedDisplayClientTests.Definition(emulator.Port), ElitelLedDisplayClientTests.FastOptions(), NullLogger.Instance);
        var frame = LedSigns.Number(64, 32, 42, LedColor.Green);

        emulator.SimulateRestart();
        await Assert.ThrowsAsync<LedDisplayRestartedException>(() => client.ShowAsync(frame));

        await client.ShowAsync(frame);
        Assert.Equal(new[] { "42" }, emulator.Screen.TextLines);
    }

    [Fact]
    public async Task Manager_SendsTheFrameAgainAfterARestartNoticedByTheProbe()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 64, 32).Start();
        var (manager, clock) = LedDisplayManagerTests.CreateManager(ElitelLedDisplayClientTests.Definition(emulator.Port));

        await manager.ShowAsync("entry", LedSigns.Number(64, 32, 42, LedColor.Green));
        emulator.SimulateRestart();

        // The probe (60 s) gets RESET OK instead of its OK - the board is blank and the manager knows it.
        clock.Advance(TimeSpan.FromSeconds(61));
        await manager.RunMaintenanceAsync();
        Assert.Empty(emulator.Screen.TextLines);

        // The next round (retry interval 15 s) sends the frame again.
        clock.Advance(TimeSpan.FromSeconds(16));
        await manager.RunMaintenanceAsync();
        Assert.Equal(new[] { "42" }, emulator.Screen.TextLines);
    }
}
