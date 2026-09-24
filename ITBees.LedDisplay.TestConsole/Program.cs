using ITBees.LedDisplay.Models;
using ITBees.LedDisplay.Protocol;
using ITBees.LedDisplay.Rendering;
using ITBees.LedDisplay.Services;
using Microsoft.Extensions.Logging;

// Commissioning tool for a real board (or the emulator) - a Docklight replacement that knows the
// Windows-1250 encoding and the frame layout rules of the library.
const string usage = """
    Usage: ITBees.LedDisplay.TestConsole <host[:port]> <width>x<height> <verb> [arguments]

      text "<message>" [colour]            full-screen message, word-wrapped, biggest font that fits
      number <value> [colour]              full-screen number
      symbol <symbol> [colour] ["caption"] ArrowUp|ArrowDown|ArrowLeft|ArrowRight|Cross|NoEntry
      bright <1-10>                        10 = automatic
      clear
      raw "<line>" ["<line>" ...]          protocol lines as they are, e.g. "FONT;3" "SET;0;0;\"123\""
      demo                                 cycles through sample frames every 3 s

    Colours: Red Green Yellow Blue Magenta Cyan White.   Port defaults to 3000.
    Example: ITBees.LedDisplay.TestConsole 192.168.1.50 64x32 symbol NoEntry Red "TYLKO REZERWACJE"
    """;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Length < 3)
{
    Console.WriteLine(usage);
    return 1;
}

var endpoint = args[0].Split(':');
var size = args[1].ToLowerInvariant().Split('x');
var definition = new LedDisplayDefinition
{
    Id = "test",
    Name = "test",
    Host = endpoint[0],
    Port = endpoint.Length > 1 ? int.Parse(endpoint[1]) : ElitelCommands.DefaultPort,
    Width = int.Parse(size[0]),
    Height = int.Parse(size[1]),
};

var client = new ElitelLedDisplayClient(definition, new LedDisplayOptions(), new ConsoleLogger());
var verb = args[2].ToLowerInvariant();
var rest = args.Skip(3).ToArray();

LedColor ColourAt(int index, LedColor fallback) =>
    rest.Length > index ? Enum.Parse<LedColor>(rest[index], ignoreCase: true) : fallback;

try
{
    switch (verb)
    {
        case "text":
            await client.ShowAsync(LedSigns.Message(definition.Width, definition.Height, rest[0], ColourAt(1, LedColor.Yellow)));
            break;
        case "number":
            await client.ShowAsync(LedSigns.Number(definition.Width, definition.Height, int.Parse(rest[0]), ColourAt(1, LedColor.Green)));
            break;
        case "symbol":
            var symbol = Enum.Parse<LedSymbol>(rest[0], ignoreCase: true);
            var caption = rest.Length > 2 ? rest[2] : null;
            await client.ShowAsync(LedSigns.Symbol(definition.Width, definition.Height, symbol, ColourAt(1, LedColor.Red), caption));
            break;
        case "bright":
            await client.ExecuteAsync(new[] { ElitelCommands.Brightness(int.Parse(rest[0])) });
            break;
        case "clear":
            await client.ExecuteAsync(new[] { ElitelCommands.Clear() });
            break;
        case "raw":
            await client.ExecuteAsync(rest.Select(x => new LedCommand(x)).ToList());
            break;
        case "demo":
            var frames = new[]
            {
                LedSigns.Number(definition.Width, definition.Height, 123, LedColor.Green),
                LedSigns.Message(definition.Width, definition.Height, "PARKING ZAMKNIĘTY", LedColor.Red),
                LedSigns.Symbol(definition.Width, definition.Height, LedSymbol.ArrowDown, LedColor.Green),
                LedSigns.Symbol(definition.Width, definition.Height, LedSymbol.Cross, LedColor.Red),
                LedSigns.Symbol(definition.Width, definition.Height, LedSymbol.NoEntry, LedColor.Red, "TYLKO REZERWACJE", LedColor.White),
                LedSigns.Message(definition.Width, definition.Height, "ąćęłńóśźż ĄĆĘŁŃÓŚŹŻ", LedColor.White),
            };
            foreach (var frame in frames)
            {
                Console.WriteLine($"== {frame.Description}");
                await client.ShowAsync(frame);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            break;
        default:
            Console.WriteLine(usage);
            return 1;
    }

    Console.WriteLine("Done.");
    return 0;
}
catch (LedDisplayException e)
{
    Console.WriteLine($"FAILED: {e.Message}");
    return 2;
}

internal sealed class ConsoleLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
}
