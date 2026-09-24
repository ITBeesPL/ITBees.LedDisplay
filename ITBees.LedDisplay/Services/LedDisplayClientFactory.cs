using ITBees.LedDisplay.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ITBees.LedDisplay.Services;

public sealed class LedDisplayClientFactory : ILedDisplayClientFactory
{
    private readonly LedDisplayOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public LedDisplayClientFactory(IOptions<LedDisplayOptions> options, ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _loggerFactory = loggerFactory;
    }

    public ILedDisplayClient Create(LedDisplayDefinition definition) => definition.Protocol switch
    {
        LedDisplayProtocol.Elitel => new ElitelLedDisplayClient(
            definition,
            _options,
            _loggerFactory.CreateLogger("ITBees.LedDisplay.Elitel")),
        _ => throw new NotSupportedException($"LED display protocol {definition.Protocol} is not supported"),
    };
}
