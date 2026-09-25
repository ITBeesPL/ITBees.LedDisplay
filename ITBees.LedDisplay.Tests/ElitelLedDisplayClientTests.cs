using System.Net;
using System.Net.Sockets;
using ITBees.LedDisplay.DeviceEmulator;
using ITBees.LedDisplay.Models;
using ITBees.LedDisplay.Protocol;
using ITBees.LedDisplay.Rendering;
using ITBees.LedDisplay.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ITBees.LedDisplay.Tests;

public class ElitelLedDisplayClientTests
{
    internal static LedDisplayOptions FastOptions() => new()
    {
        ConnectTimeout = TimeSpan.FromSeconds(2),
        CommandTimeout = TimeSpan.FromMilliseconds(500),
    };

    internal static LedDisplayDefinition Definition(int port, int width = 64, int height = 32) => new()
    {
        Id = "entry",
        Name = "Entry board",
        Host = "127.0.0.1",
        Port = port,
        Width = width,
        Height = height,
    };

    [Fact]
    public async Task ShowAsync_DrawsTheFrameOnTheBoard()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 64, 32).Start();
        var client = new ElitelLedDisplayClient(Definition(emulator.Port), FastOptions(), NullLogger.Instance);

        await client.ShowAsync(LedSigns.Message(64, 32, "PARKING ZAMKNIĘTY", LedColor.Red, brightness: 6));

        Assert.Equal(new[] { "PARKING", "ZAMKNIĘTY" }, emulator.Screen.TextLines);
        Assert.Equal(LedColor.Red, emulator.Screen.Color);
        Assert.Equal(6, emulator.Screen.Brightness);
    }

    [Fact]
    public async Task ShowAsync_DrawsSymbolsPixelByPixel()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 32, 32).Start();
        var client = new ElitelLedDisplayClient(Definition(emulator.Port, 32, 32), FastOptions(), NullLogger.Instance);
        var frame = LedSigns.Symbol(32, 32, LedSymbol.Cross, LedColor.Red);

        await client.ShowAsync(frame);

        Assert.Equal(frame.Pixels.Count, emulator.Screen.LitPixelCount);
        Assert.Equal(LedColor.Red, emulator.Screen.GetPixel(15, 15));
        Assert.Equal(LedColor.Off, emulator.Screen.GetPixel(15, 2));
    }

    [Fact]
    public async Task ShowAsync_ReplacesThePreviousContent()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 64, 32).Start();
        var client = new ElitelLedDisplayClient(Definition(emulator.Port), FastOptions(), NullLogger.Instance);

        await client.ShowAsync(LedSigns.Symbol(64, 32, LedSymbol.ArrowDown, LedColor.Green));
        await client.ShowAsync(LedSigns.Number(64, 32, 7, LedColor.Green));

        Assert.Equal(0, emulator.Screen.LitPixelCount);
        Assert.Equal(new[] { "7" }, emulator.Screen.TextLines);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenTheBoardRejectsACommand()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 64, 32).Start();
        emulator.ResponseMode = EmulatorResponseMode.RejectAll;
        var client = new ElitelLedDisplayClient(Definition(emulator.Port), FastOptions(), NullLogger.Instance);

        var exception = await Assert.ThrowsAsync<LedDisplayCommandRejectedException>(
            () => client.ExecuteAsync(new[] { ElitelCommands.Clear(), ElitelCommands.Color(LedColor.Red) }));

        Assert.Equal("CLEAR", exception.Command);
        Assert.Equal("ERR", exception.Response);
        Assert.Equal(new[] { "CLEAR" }, emulator.ReceivedLines);
    }

    [Fact]
    public async Task ExecuteAsync_TimesOutWhenTheBoardDoesNotAnswer()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 64, 32).Start();
        emulator.ResponseMode = EmulatorResponseMode.Silent;
        var client = new ElitelLedDisplayClient(Definition(emulator.Port), FastOptions(), NullLogger.Instance);

        var exception = await Assert.ThrowsAsync<LedDisplayException>(() => client.ExecuteAsync(new[] { ElitelCommands.Clear() }));

        Assert.Contains("did not confirm 'CLEAR'", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsWhenNothingListens()
    {
        var client = new ElitelLedDisplayClient(Definition(UnusedPort()), FastOptions(), NullLogger.Instance);

        await Assert.ThrowsAsync<LedDisplayException>(() => client.ExecuteAsync(new[] { ElitelCommands.Clear() }));
    }

    [Fact]
    public async Task ShowAsync_RefusesAFrameBuiltForAnotherScreenSize()
    {
        var client = new ElitelLedDisplayClient(Definition(UnusedPort()), FastOptions(), NullLogger.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => client.ShowAsync(LedSigns.Number(32, 16, 1, LedColor.Green)));
    }

    internal static int UnusedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
