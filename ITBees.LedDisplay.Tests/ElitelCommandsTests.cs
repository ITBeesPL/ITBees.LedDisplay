using ITBees.LedDisplay.Protocol;
using Xunit;

namespace ITBees.LedDisplay.Tests;

public class ElitelCommandsTests
{
    private static byte[] Hex(string hex) => hex.Split(' ').Select(x => Convert.ToByte(x, 16)).ToArray();

    [Fact]
    public void SetText_EncodesPolishLowercaseLikeTheVendorScript()
    {
        // Vendor test script: set;0;0;"ąćęłńóśżź" - the script sends the command name in lower case.
        var vendor = Hex("73 65 74 3B 30 3B 30 3B 22 B9 E6 EA B3 F1 F3 9C BF 9F 22 0A");

        var bytes = ElitelCommands.SetText(0, 0, "ąćęłńóśżź").ToBytes();

        Assert.Equal("SET"u8.ToArray(), bytes[..3]);
        Assert.Equal(vendor[3..], bytes[3..]);
    }

    [Fact]
    public void SetText_EncodesPolishUppercaseLikeTheVendorScript()
    {
        var vendor = Hex("73 65 74 3B 30 3B 30 3B 22 A5 C6 CA A3 D1 D3 8C AF 8F 22 0A");

        var bytes = ElitelCommands.SetText(0, 0, "ĄĆĘŁŃÓŚŻŹ").ToBytes();

        Assert.Equal(vendor[3..], bytes[3..]);
    }

    [Fact]
    public void SetText_PutsRowBeforeColumn()
    {
        // Vendor script: set;30;0;"ABCabc" draws at row 30, column 0.
        Assert.Equal("SET;30;0;\"ABCabc\"", ElitelCommands.SetText(x: 0, y: 30, "ABCabc").Line);
    }

    [Fact]
    public void Pixel_PutsRowBeforeColumn()
    {
        // Vendor script: pix;31;63;4 is the bottom-right corner of the 64x32 test board.
        Assert.Equal("PIX;31;63;4", ElitelCommands.Pixel(x: 63, y: 31, LedColor.Blue).Line);
    }

    [Theory]
    [InlineData("say \"hi\"", "say 'hi'")]
    [InlineData("a;b", "a,b")]
    [InlineData("line1\nline2", "line1 line2")]
    [InlineData("„cudzysłów”", "'cudzysłów'")]
    public void SetText_NeutralisesCharactersThatWouldBreakTheLine(string text, string expected)
    {
        Assert.Equal($"SET;0;0;\"{expected}\"", ElitelCommands.SetText(0, 0, text).Line);
    }

    [Fact]
    public void SetText_ReplacesCharactersOutsideTheCodePage()
    {
        var bytes = ElitelCommands.SetText(0, 0, "a✓b").ToBytes();

        Assert.Equal("SET;0;0;\"a?b\"\n"u8.ToArray(), bytes);
    }

    [Fact]
    public void SimpleCommands_MatchTheDocumentation()
    {
        Assert.Equal("CLEAR\n"u8.ToArray(), ElitelCommands.Clear().ToBytes());
        Assert.Equal("COLOR;7", ElitelCommands.Color(LedColor.White).Line);
        Assert.Equal("FONT;4", ElitelCommands.Font(LedFont.Font12x24).Line);
        Assert.Equal("BRIGHT;6", ElitelCommands.Brightness(6).Line);
        Assert.Equal("BRIGHT;10", ElitelCommands.Brightness(LedBrightness.Auto).Line);
    }

    [Fact]
    public void Commands_RejectValuesOutsideTheProtocol()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ElitelCommands.Color(LedColor.Off));
        Assert.Throws<ArgumentOutOfRangeException>(() => ElitelCommands.Brightness(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ElitelCommands.Brightness(11));
        Assert.Throws<ArgumentOutOfRangeException>(() => ElitelCommands.Font((LedFont)5));
        Assert.Throws<ArgumentOutOfRangeException>(() => ElitelCommands.SetText(-1, 0, "x"));
    }
}
