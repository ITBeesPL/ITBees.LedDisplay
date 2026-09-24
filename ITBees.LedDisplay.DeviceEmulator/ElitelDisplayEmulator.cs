using System.Net;
using System.Net.Sockets;
using ITBees.LedDisplay.Protocol;

namespace ITBees.LedDisplay.DeviceEmulator;

/// <summary>How the emulator answers - lets tests reproduce a misbehaving board.</summary>
public enum EmulatorResponseMode
{
    Normal,

    /// <summary>Reads commands but never answers.</summary>
    Silent,

    /// <summary>Answers every command with <c>ERR</c>.</summary>
    RejectAll,
}

/// <summary>TCP server speaking the Elitel line protocol on top of an <see cref="EmulatedScreen"/>.</summary>
public sealed class ElitelDisplayEmulator : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly List<string> _receivedLines = new();
    private Task? _acceptLoop;
    private int _connectionCount;

    public ElitelDisplayEmulator(int port, int width, int height)
    {
        Screen = new EmulatedScreen(width, height);
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public EmulatedScreen Screen { get; }
    public EmulatorResponseMode ResponseMode { get; set; } = EmulatorResponseMode.Normal;
    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public int ConnectionCount => Volatile.Read(ref _connectionCount);

    /// <summary>Raised after every applied command with the line received.</summary>
    public event Action<string>? CommandReceived;

    public IReadOnlyList<string> ReceivedLines
    {
        get
        {
            lock (_receivedLines)
            {
                return _receivedLines.ToList();
            }
        }
    }

    /// <summary>Starts listening; port 0 picks a free one (read it from <see cref="Port"/>).</summary>
    public ElitelDisplayEmulator Start()
    {
        _listener.Start();
        _acceptLoop = AcceptLoop();
        return this;
    }

    public void ClearReceivedLines()
    {
        lock (_receivedLines)
        {
            _receivedLines.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
        _listener.Stop();
        if (_acceptLoop != null)
        {
            try
            {
                await _acceptLoop;
            }
            catch (Exception)
            {
                // Stopping the listener faults the pending accept - expected on shutdown.
            }
        }
    }

    private async Task AcceptLoop()
    {
        while (_stopping.IsCancellationRequested == false)
        {
            var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
            Interlocked.Increment(ref _connectionCount);
            _ = Task.Run(() => Serve(client));
        }
    }

    private async Task Serve(TcpClient client)
    {
        using var _ = client;
        var stream = client.GetStream();
        var buffer = new byte[4096];
        var pending = new List<byte>();
        try
        {
            while (_stopping.IsCancellationRequested == false)
            {
                var read = await stream.ReadAsync(buffer, _stopping.Token);
                if (read == 0)
                {
                    return;
                }

                pending.AddRange(buffer.Take(read));
                int newLine;
                while ((newLine = pending.IndexOf((byte)'\n')) >= 0)
                {
                    var line = ElitelTextEncoding.Encoding.GetString(pending.Take(newLine).ToArray()).TrimEnd('\r');
                    pending.RemoveRange(0, newLine + 1);
                    lock (_receivedLines)
                    {
                        _receivedLines.Add(line);
                    }

                    var reply = ResponseMode switch
                    {
                        EmulatorResponseMode.Silent => null,
                        EmulatorResponseMode.RejectAll => "ERR",
                        _ => Screen.Execute(line),
                    };

                    CommandReceived?.Invoke(line);
                    if (reply != null)
                    {
                        await stream.WriteAsync(ElitelTextEncoding.Encoding.GetBytes(reply + "\n"), _stopping.Token);
                    }
                }
            }
        }
        catch (Exception) when (_stopping.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
            // Client went away.
        }
    }
}
