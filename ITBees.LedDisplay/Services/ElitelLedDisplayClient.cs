using System.Net.Sockets;
using ITBees.LedDisplay.Models;
using ITBees.LedDisplay.Protocol;
using ITBees.LedDisplay.Rendering;
using Microsoft.Extensions.Logging;

namespace ITBees.LedDisplay.Services;

/// <summary>
/// Elitel protocol client. Every batch (a frame, a probe) gets its own TCP connection: a board that
/// rebooted or dropped off the network between two updates never leaves a half-open socket behind,
/// and the board stays free for the vendor's web console and test tools in between.
/// </summary>
/// <remarks>
/// Replies carry no correlation to the command, only their order on the wire does. Bytes waiting in
/// the socket before a command goes out are therefore discarded, and any failure ends the connection -
/// the next batch starts on a fresh one.
/// </remarks>
public sealed class ElitelLedDisplayClient : ILedDisplayClient
{
    private const int MaxResponseLength = 256;

    private readonly LedDisplayOptions _options;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ElitelLedDisplayClient(LedDisplayDefinition definition, LedDisplayOptions options, ILogger logger)
    {
        definition.Validate();
        Definition = definition;
        _options = options;
        _logger = logger;
    }

    public LedDisplayDefinition Definition { get; }

    public Task ShowAsync(LedFrame frame, CancellationToken cancellationToken = default)
    {
        if (frame.Width != Definition.Width || frame.Height != Definition.Height)
        {
            throw new ArgumentException(
                $"Frame is {frame.Width}x{frame.Height} but LED display '{Definition.Id}' is {Definition.Width}x{Definition.Height}");
        }

        return ExecuteAsync(ElitelFrameCompiler.Compile(frame), cancellationToken);
    }

    public Task ProbeAsync(LedFrame? currentFrame, CancellationToken cancellationToken = default)
    {
        // Repeating the current colour changes nothing on the screen but still needs a working controller
        // to answer. With nothing known about the screen, accepting the connection is all we check.
        var commands = currentFrame == null
            ? Array.Empty<LedCommand>()
            : new[] { ElitelCommands.Color(currentFrame.Color) };
        return ExecuteAsync(commands, cancellationToken);
    }

    public async Task ExecuteAsync(IReadOnlyList<LedCommand> commands, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var tcpClient = new TcpClient { NoDelay = true };
            await ConnectAsync(tcpClient, cancellationToken);
            var stream = tcpClient.GetStream();
            var reader = new ResponseReader(stream);

            foreach (var command in commands)
            {
                await DiscardStaleInput(stream, reader, cancellationToken);

                _logger.LogDebug("LED {DisplayId} TX {Command}", Definition.Id, command.Line);
                var response = await WithTimeout(
                    async token =>
                    {
                        await stream.WriteAsync(command.ToBytes(), token);
                        return await reader.ReadLineAsync(token);
                    },
                    $"'{command.Line}'",
                    cancellationToken);
                _logger.LogDebug("LED {DisplayId} RX {Response}", Definition.Id, response);

                if (string.Equals(response, "OK", StringComparison.OrdinalIgnoreCase) == false)
                {
                    throw new LedDisplayCommandRejectedException(Definition.Id, command.Line, response);
                }
            }
        }
        catch (LedDisplayException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e) when (e is SocketException or IOException or ObjectDisposedException)
        {
            throw new LedDisplayException(Definition.Id, $"LED display '{Definition}' communication failed: {e.Message}", e);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ConnectAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ConnectTimeout);
        try
        {
            await tcpClient.ConnectAsync(Definition.Host, Definition.Port, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
        {
            throw new LedDisplayException(
                Definition.Id,
                $"LED display '{Definition}' did not accept a connection within {_options.ConnectTimeout.TotalSeconds:0.#} s");
        }
    }

    private async Task<string> WithTimeout(
        Func<CancellationToken, Task<string>> operation,
        string what,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.CommandTimeout);
        try
        {
            return await operation(timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested == false)
        {
            throw new LedDisplayException(
                Definition.Id,
                $"LED display '{Definition}' did not confirm {what} within {_options.CommandTimeout.TotalSeconds:0.#} s");
        }
    }

    private async Task DiscardStaleInput(NetworkStream stream, ResponseReader reader, CancellationToken cancellationToken)
    {
        var stale = reader.TakeBuffered();
        var buffer = new byte[MaxResponseLength];
        while (stream.DataAvailable)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            stale += ElitelTextEncoding.Encoding.GetString(buffer, 0, read);
        }

        if (stale.Length > 0)
        {
            _logger.LogWarning("LED {DisplayId} discarded unexpected input '{Stale}'", Definition.Id, stale.Trim());
        }
    }

    /// <summary>Reads <c>\n</c>-terminated lines, keeping whatever arrived after the line for later.</summary>
    private sealed class ResponseReader
    {
        private readonly NetworkStream _stream;
        private readonly byte[] _buffer = new byte[MaxResponseLength];
        private int _length;

        public ResponseReader(NetworkStream stream)
        {
            _stream = stream;
        }

        public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                var newLine = Array.IndexOf(_buffer, (byte)'\n', 0, _length);
                if (newLine >= 0)
                {
                    var line = ElitelTextEncoding.Encoding.GetString(_buffer, 0, newLine);
                    Buffer.BlockCopy(_buffer, newLine + 1, _buffer, 0, _length - newLine - 1);
                    _length -= newLine + 1;
                    return line.Trim();
                }

                if (_length == _buffer.Length)
                {
                    throw new IOException($"Response longer than {MaxResponseLength} bytes without a line feed");
                }

                var read = await _stream.ReadAsync(_buffer.AsMemory(_length), cancellationToken);
                if (read == 0)
                {
                    throw new IOException("Connection closed by the display");
                }

                _length += read;
            }
        }

        public string TakeBuffered()
        {
            var text = _length == 0 ? string.Empty : ElitelTextEncoding.Encoding.GetString(_buffer, 0, _length);
            _length = 0;
            return text;
        }
    }
}
