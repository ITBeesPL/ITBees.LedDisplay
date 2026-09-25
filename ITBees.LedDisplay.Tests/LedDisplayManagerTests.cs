using ITBees.LedDisplay.DeviceEmulator;
using ITBees.LedDisplay.Models;
using ITBees.LedDisplay.Protocol;
using ITBees.LedDisplay.Rendering;
using ITBees.LedDisplay.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ITBees.LedDisplay.Tests;

public class LedDisplayManagerTests
{
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 23, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static (LedDisplayManager Manager, ManualTimeProvider Clock) CreateManager(params LedDisplayDefinition[] displays)
    {
        var options = ElitelLedDisplayClientTests.FastOptions();
        options.ProbeInterval = TimeSpan.FromSeconds(60);
        options.RefreshInterval = TimeSpan.FromMinutes(15);
        options.RetryInterval = TimeSpan.FromSeconds(15);
        options.Displays = displays.ToList();

        var wrapped = Options.Create(options);
        var clock = new ManualTimeProvider();
        var manager = new LedDisplayManager(
            wrapped,
            new LedDisplayClientFactory(wrapped, NullLoggerFactory.Instance),
            clock,
            NullLogger<LedDisplayManager>.Instance);
        return (manager, clock);
    }

    [Fact]
    public async Task ShowAsync_DoesNotResendAnIdenticalFrame()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 64, 32).Start();
        var (manager, _) = CreateManager(ElitelLedDisplayClientTests.Definition(emulator.Port));

        var first = await manager.ShowAsync("entry", LedSigns.Number(64, 32, 12, LedColor.Green));
        var second = await manager.ShowAsync("entry", LedSigns.Number(64, 32, 12, LedColor.Green));

        Assert.Equal(LedDisplayState.Online, first.State);
        Assert.Equal(LedDisplayState.Online, second.State);
        Assert.Equal(1, emulator.ConnectionCount);
        Assert.Equal("12", second.ShownDescription);
    }

    [Fact]
    public async Task ShowAsync_ReportsOfflineWithoutThrowingAndRetriesInTheBackground()
    {
        var port = ElitelLedDisplayClientTests.UnusedPort();
        var (manager, clock) = CreateManager(ElitelLedDisplayClientTests.Definition(port));
        var changes = new List<LedDisplayStatusChangedEventArgs>();
        manager.StatusChanged += (_, e) => changes.Add(e);

        var offline = await manager.ShowAsync("entry", LedSigns.Message(64, 32, "PARKING ZAMKNIĘTY", LedColor.Red));

        Assert.Equal(LedDisplayState.Offline, offline.State);
        Assert.True(offline.IsFramePending);
        Assert.Equal(1, offline.ConsecutiveFailures);
        Assert.NotNull(offline.LastError);

        // The board comes up on its address - the retry is due only after RetryInterval.
        await using var emulator = new ElitelDisplayEmulator(port, 64, 32).Start();
        await manager.RunMaintenanceAsync();
        Assert.Equal(0, emulator.ConnectionCount);

        clock.Advance(TimeSpan.FromSeconds(15));
        await manager.RunMaintenanceAsync();

        var online = manager.GetStatus("entry")!;
        Assert.Equal(LedDisplayState.Online, online.State);
        Assert.False(online.IsFramePending);
        Assert.Equal(new[] { "PARKING", "ZAMKNIĘTY" }, emulator.Screen.TextLines);
        Assert.Equal(
            new[] { (LedDisplayState.Unknown, LedDisplayState.Offline), (LedDisplayState.Offline, LedDisplayState.Online) },
            changes.Select(x => (x.PreviousState, x.Status.State)));
    }

    [Fact]
    public async Task Maintenance_ProbesWithoutTouchingTheScreenAndRedrawsAfterAnOutage()
    {
        var port = ElitelLedDisplayClientTests.UnusedPort();
        var (manager, clock) = CreateManager(ElitelLedDisplayClientTests.Definition(port));

        var emulator = new ElitelDisplayEmulator(port, 64, 32).Start();
        await manager.ShowAsync("entry", LedSigns.Number(64, 32, 99, LedColor.Green));
        emulator.ClearReceivedLines();

        clock.Advance(TimeSpan.FromSeconds(60));
        await manager.RunMaintenanceAsync();
        Assert.Equal(new[] { "COLOR;2" }, emulator.ReceivedLines);

        // The board loses power and comes back blank.
        await emulator.DisposeAsync();
        clock.Advance(TimeSpan.FromSeconds(60));
        await manager.RunMaintenanceAsync();
        Assert.Equal(LedDisplayState.Offline, manager.GetStatus("entry")!.State);

        await using var restarted = new ElitelDisplayEmulator(port, 64, 32).Start();
        clock.Advance(TimeSpan.FromSeconds(15));
        await manager.RunMaintenanceAsync();

        Assert.Equal(LedDisplayState.Online, manager.GetStatus("entry")!.State);
        Assert.Equal(new[] { "99" }, restarted.Screen.TextLines);
    }

    [Fact]
    public async Task Maintenance_ResendsTheFramePeriodically()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 64, 32).Start();
        var (manager, clock) = CreateManager(ElitelLedDisplayClientTests.Definition(emulator.Port));
        await manager.ShowAsync("entry", LedSigns.Number(64, 32, 5, LedColor.Green));
        emulator.ClearReceivedLines();

        clock.Advance(TimeSpan.FromMinutes(15));
        await manager.RunMaintenanceAsync();

        Assert.Contains("CLEAR", emulator.ReceivedLines);
        Assert.Contains("SET;4;26;\"5\"", emulator.ReceivedLines);
    }

    [Fact]
    public async Task Configure_KeepsTheStateOfUnchangedBoardsAndDropsRemovedOnes()
    {
        await using var emulator = new ElitelDisplayEmulator(0, 64, 32).Start();
        var entry = ElitelLedDisplayClientTests.Definition(emulator.Port);
        var (manager, _) = CreateManager(entry);
        await manager.ShowAsync("entry", LedSigns.Number(64, 32, 5, LedColor.Green));

        manager.Configure(new[] { entry with { }, entry with { Id = "exit", Name = "Exit board" } });

        Assert.Equal(LedDisplayState.Online, manager.GetStatus("entry")!.State);
        Assert.Equal(LedDisplayState.Unknown, manager.GetStatus("exit")!.State);

        manager.Configure(new[] { entry with { Id = "exit" } });

        Assert.Null(manager.GetStatus("entry"));
        Assert.Throws<ArgumentException>(() => manager.Configure(new[] { entry, entry with { } }));
    }

    [Fact]
    public async Task ShowAsync_ThrowsForAnUnknownBoard()
    {
        var (manager, _) = CreateManager();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.ShowAsync("nope", LedFrame.Blank(64, 32)));
    }
}
