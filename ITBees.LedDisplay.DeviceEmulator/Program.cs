using ITBees.LedDisplay.DeviceEmulator;

// Usage: dotnet run --project ITBees.LedDisplay.DeviceEmulator -- [port=3000] [width=64] [height=32]
Console.OutputEncoding = System.Text.Encoding.UTF8;
var port = args.Length > 0 ? int.Parse(args[0]) : 3000;
var width = args.Length > 1 ? int.Parse(args[1]) : 64;
var height = args.Length > 2 ? int.Parse(args[2]) : 32;

await using var emulator = new ElitelDisplayEmulator(port, width, height).Start();
Console.WriteLine($"Elitel LED board emulator {width}x{height} listening on 127.0.0.1:{emulator.Port}. Ctrl+C stops.");

// Redraw once a burst of commands is over, not after each of the hundreds of PIX lines a symbol takes.
var redraw = new Timer(_ => Console.WriteLine(emulator.Screen.Render()));
emulator.CommandReceived += line =>
{
    Console.WriteLine($"<< {line}");
    redraw.Change(TimeSpan.FromMilliseconds(200), Timeout.InfiniteTimeSpan);
};

var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stop.TrySetResult();
};
await stop.Task;
