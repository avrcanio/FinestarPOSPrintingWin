using System.Net;
using System.Text;
using System.Text.Json;
using MozzartPrintHub.WinForms.Domain;

namespace MozzartPrintHub.WinForms.Services;

public sealed class HttpReceiverServer
{
    private readonly HttpListener _listener = new();
    private readonly EmulatorStore _store;
    private readonly Func<byte[], string, Task> _onEscPosPayload;
    private readonly string? _token;
    private CancellationTokenSource? _cts;

    public HttpReceiverServer(
        string bind,
        int port,
        EmulatorStore store,
        Func<byte[], string, Task> onEscPosPayload,
        string? token)
    {
        var host = bind switch
        {
            "0.0.0.0" => "+",
            "*" => "+",
            _ => bind
        };

        var prefix = $"http://{host}:{port}/";
        _listener.Prefixes.Add(prefix);
        _store = store;
        _onEscPosPayload = onEscPosPayload;
        _token = token;
    }

    public Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _listener.Start();
        return Task.Run(() => LoopAsync(_cts.Token), _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener.Stop();
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(ctx), ct);
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "/";

        if (ctx.Request.HttpMethod == "GET" && path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(ctx.Response, 200, new { status = "ok" });
            return;
        }

        if (!await AuthorizeAsync(ctx))
        {
            return;
        }

        if (ctx.Request.HttpMethod == "GET" && path.Equals("/emulator/last", StringComparison.OrdinalIgnoreCase))
        {
            var item = _store.GetLast();
            if (item is null)
            {
                await WriteJsonAsync(ctx.Response, 404, new { code = "no_jobs" });
                return;
            }

            await WriteJsonAsync(ctx.Response, 200, item);
            return;
        }

        if (ctx.Request.HttpMethod == "GET" && path.Equals("/emulator/history", StringComparison.OrdinalIgnoreCase))
        {
            var take = 50;
            _ = int.TryParse(ctx.Request.QueryString["take"], out take);
            var items = _store.GetHistory(take);
            await WriteJsonAsync(ctx.Response, 200, items);
            return;
        }

        if (ctx.Request.HttpMethod == "POST" && path.Equals("/print", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            var rawJson = await reader.ReadToEndAsync();
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(rawJson);
            }
            catch (JsonException)
            {
                await WriteJsonAsync(ctx.Response, 400, new { code = "invalid_json" });
                return;
            }

            using (doc)
            {
            var kind = doc.RootElement.TryGetProperty("kind", out var kindNode) ? kindNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(kind))
            {
                await WriteJsonAsync(ctx.Response, 400, new { code = "invalid_payload" });
                return;
            }

            if (kind == "bar_ticket")
            {
                var bytes = Encoding.ASCII.GetBytes(ExtractBarTicketText(doc.RootElement) + "\n");
                await _onEscPosPayload(bytes, $"http:{ctx.Request.RemoteEndPoint}");
                await WriteJsonAsync(ctx.Response, 200, new { status = "printed" });
                return;
            }

            if (kind == "receipt_pdf")
            {
                var item = new EmulatorJob
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Source = "http",
                    ClientIp = ctx.Request.RemoteEndPoint?.ToString() ?? "unknown",
                    RawSizeBytes = rawJson.Length,
                    ReceivedAtUtc = DateTime.UtcNow,
                    Document = new ParsedEscPosDocument()
                };
                item.Document.Lines.Add(new ParsedLine { Text = "[PDF RECEIPT RECEIVED]", Align = "left", Bold = true });
                item.PrintStatus = "received_no_preview";
                _store.Add(item);
                await WriteJsonAsync(ctx.Response, 200, new { status = "accepted" });
                return;
            }

            await WriteJsonAsync(ctx.Response, 400, new { code = "unsupported_kind" });
            return;
            }
        }

        await WriteJsonAsync(ctx.Response, 404, new { code = "not_found" });
    }

    private async Task<bool> AuthorizeAsync(HttpListenerContext ctx)
    {
        if (string.IsNullOrWhiteSpace(_token))
        {
            return true;
        }

        var provided = ctx.Request.Headers["X-Bridge-Token"];
        if (string.IsNullOrWhiteSpace(provided))
        {
            await WriteJsonAsync(ctx.Response, 401, new { code = "missing_token", message = "X-Bridge-Token header is required" });
            return false;
        }

        if (!string.Equals(provided, _token, StringComparison.Ordinal))
        {
            await WriteJsonAsync(ctx.Response, 403, new { code = "invalid_token", message = "X-Bridge-Token is invalid" });
            return false;
        }

        return true;
    }

    private static string ExtractBarTicketText(JsonElement root)
    {
        if (!root.TryGetProperty("payload", out var payload))
        {
            return "BAR TICKET";
        }

        var table = payload.TryGetProperty("table", out var tableNode) ? tableNode.GetString() : "N/A";
        var waiter = payload.TryGetProperty("waiter", out var waiterNode) ? waiterNode.GetString() : "N/A";
        return $"Table: {table} | Waiter: {waiter}";
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, int statusCode, object body)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true });
        var data = Encoding.UTF8.GetBytes(json);
        await response.OutputStream.WriteAsync(data);
        response.Close();
    }
}
