using System.Net;
using System.Net.Sockets;

namespace MozzartPrintHub.WinForms.Services;

public sealed class TcpEscPosServer
{
    private readonly string _bind;
    private readonly int _port;
    private readonly int _maxJobBytes;
    private readonly TimeSpan _idleReadTimeout;
    private readonly Func<byte[], string, Task> _onPayload;
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;
    private Task? _listenTask;

    public TcpEscPosServer(
        string bind,
        int port,
        int maxJobBytes,
        TimeSpan idleReadTimeout,
        Func<byte[], string, Task> onPayload)
    {
        _bind = bind;
        _port = port;
        _maxJobBytes = maxJobBytes;
        _idleReadTimeout = idleReadTimeout;
        _onPayload = onPayload;
    }

    public Task StartAsync()
    {
        var ip = _bind switch
        {
            "0.0.0.0" => IPAddress.Any,
            "*" => IPAddress.Any,
            "+" => IPAddress.Any,
            _ => IPAddress.Parse(_bind)
        };

        _listener = new TcpListener(ip, _port);
        _listener.Start();
        _listenTask = Task.Run(ListenLoopAsync, _cts.Token);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _cts.Cancel();
        _listener?.Stop();
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(_cts.Token);
                _ = Task.Run(async () =>
                {
                    await using var stream = client.GetStream();
                    using var ms = new MemoryStream();
                    var buffer = new byte[4096];
                    while (true)
                    {
                        var readTask = stream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cts.Token).AsTask();
                        var timeoutTask = Task.Delay(_idleReadTimeout, _cts.Token);
                        var done = await Task.WhenAny(readTask, timeoutTask);
                        if (done == timeoutTask)
                        {
                            break;
                        }

                        var read = await readTask;
                        if (read <= 0)
                        {
                            break;
                        }

                        ms.Write(buffer, 0, read);
                        if (ms.Length > _maxJobBytes)
                        {
                            break;
                        }
                    }

                    if (ms.Length > 0)
                    {
                        var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                        await _onPayload(ms.ToArray(), $"tcp:{endpoint}");
                    }

                    client.Dispose();
                }, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(500, _cts.Token);
            }
        }
    }
}
