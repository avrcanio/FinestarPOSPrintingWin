using System.Net;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MozzartPrintHub.WinForms.Domain;

namespace MozzartPrintHub.WinForms.Services;

public sealed class HttpReceiverServer
{
    private readonly string _prefix;
    private readonly EmulatorStore _store;
    private readonly Func<byte[], string, Task> _onEscPosPayload;
    private readonly ReceiptPreviewService _receiptPreviewService;
    private readonly BarTicketPdfService _barTicketPdfService;
    private readonly Func<EmulatorJob, Task>? _onJobAdded;
    private readonly string? _token;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private HttpListener? _listener;

    public HttpReceiverServer(
        string bind,
        int port,
        EmulatorStore store,
        Func<byte[], string, Task> onEscPosPayload,
        ReceiptPreviewService receiptPreviewService,
        BarTicketPdfService barTicketPdfService,
        Func<EmulatorJob, Task>? onJobAdded,
        string? token)
    {
        var host = bind switch
        {
            "0.0.0.0" => "+",
            "*" => "+",
            _ => bind
        };

        _prefix = $"http://{host}:{port}/";
        _store = store;
        _onEscPosPayload = onEscPosPayload;
        _receiptPreviewService = receiptPreviewService;
        _barTicketPdfService = barTicketPdfService;
        _onJobAdded = onJobAdded;
        _token = token;
    }

    public Task StartAsync()
    {
        if (_listener is not null && _listener.IsListening)
        {
            return Task.CompletedTask;
        }

        var listener = new HttpListener();
        listener.Prefixes.Add(_prefix);
        listener.Start();
        _listener = listener;

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(listener, _cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        var listener = _listener;
        _listener = null;

        if (listener is null)
        {
            return;
        }

        try
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            try
            {
                listener.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private async Task LoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync();
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
                var barPreview = BuildBarTicketPreview(doc.RootElement);
                var barPdfBytes = _barTicketPdfService.BuildPdf(
                    barPreview.Table,
                    barPreview.Waiter,
                    barPreview.Round,
                    barPreview.Lines);
                var receiptPreview = _receiptPreviewService.BuildFromPdfBytes(barPdfBytes);
                var pdfPath = SaveTempPdf(barPdfBytes, "bar-ticket");

                var barJob = new EmulatorJob
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Source = "http",
                    ClientIp = ctx.Request.RemoteEndPoint?.ToString() ?? "unknown",
                    RawSizeBytes = rawJson.Length,
                    UnknownCommandCount = 0,
                    RequestedPrinterName = ReadString(doc.RootElement, "printer_name"),
                    ReceivedAtUtc = DateTime.UtcNow,
                    Document = receiptPreview.Document,
                    PreviewImagePath = receiptPreview.PreviewImagePath,
                    PdfPath = pdfPath,
                    PrintStatus = "queued"
                };
                _store.Add(barJob);
                if (_onJobAdded is not null)
                {
                    await _onJobAdded(barJob);
                }
                await WriteJsonAsync(ctx.Response, 200, new { status = "printed" });
                return;
            }

            if (kind == "receipt_pdf")
            {
                string? pdfBase64 = null;
                if (doc.RootElement.TryGetProperty("payload", out var payloadNode)
                    && payloadNode.ValueKind == JsonValueKind.Object
                    && payloadNode.TryGetProperty("pdf_base64", out var pdfNode))
                {
                    pdfBase64 = pdfNode.GetString();
                }

                if (!TryDecodeBase64Pdf(pdfBase64, out var receiptBytes))
                {
                    await WriteJsonAsync(ctx.Response, 400, new { code = "invalid_pdf_base64" });
                    return;
                }

                var receiptPreview = _receiptPreviewService.BuildFromPdfBytes(receiptBytes);
                var item = new EmulatorJob
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Source = "http",
                    ClientIp = ctx.Request.RemoteEndPoint?.ToString() ?? "unknown",
                    RawSizeBytes = rawJson.Length,
                    ReceivedAtUtc = DateTime.UtcNow,
                    UnknownCommandCount = 0,
                    RequestedPrinterName = ReadString(doc.RootElement, "printer_name"),
                    Document = receiptPreview.Document,
                    PreviewImagePath = receiptPreview.PreviewImagePath,
                    PdfPath = SaveTempPdf(receiptBytes, "receipt")
                };
                item.PrintStatus = "queued";
                _store.Add(item);
                if (_onJobAdded is not null)
                {
                    await _onJobAdded(item);
                }
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

    private static BarTicketPreview BuildBarTicketPreview(JsonElement root)
    {
        var document = new ParsedEscPosDocument();

        if (!root.TryGetProperty("payload", out var payload))
        {
            document.Lines.Add(new ParsedLine { Text = "BAR TICKET", Align = "left", Bold = true });
            return new BarTicketPreview(document, "N/A", "N/A", null, Array.Empty<BarTicketLine>(), 0, "none");
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            document.Lines.Add(new ParsedLine { Text = "BAR TICKET", Align = "left", Bold = true });
            return new BarTicketPreview(document, "N/A", "N/A", null, Array.Empty<BarTicketLine>(), 0, "none");
        }

        var table = payload.TryGetProperty("table", out var tableNode) ? tableNode.GetString() : "N/A";
        var waiter = payload.TryGetProperty("waiter", out var waiterNode) ? waiterNode.GetString() : "N/A";
        var round = payload.TryGetProperty("round_number", out var roundNode) ? roundNode.ToString() : null;

        document.Lines.Add(new ParsedLine { Text = $"Table: {table} | Waiter: {waiter}", Align = "left", Bold = false });
        if (!string.IsNullOrWhiteSpace(round))
        {
            document.Lines.Add(new ParsedLine { Text = $"Round: {round}", Align = "left", Bold = false });
        }

        var items = ResolveItems(payload, out var sourcePath);
        if (items.Count > 0)
        {
            var sample = string.Join(", ", items.Take(2).Select(i => $"{i.Name} x{i.Qty.ToString("0.##", CultureInfo.InvariantCulture)}"));
            Debug.WriteLine($"[bar_ticket] source={sourcePath} count={items.Count} sample={sample}");
        }
        else
        {
            var keys = string.Join(", ", payload.EnumerateObject().Select(p => p.Name));
            Debug.WriteLine($"[bar_ticket] warning: no items resolved. payload_keys=[{keys}]");
        }

        foreach (var item in items)
        {
            var qtyText = item.Qty.ToString("0.##", CultureInfo.InvariantCulture);
            var name = string.IsNullOrWhiteSpace(item.Name) ? "(unnamed)" : item.Name;
            document.Lines.Add(new ParsedLine { Text = $"{qtyText} x {name}", Align = "left", Bold = false });
            if (!string.IsNullOrWhiteSpace(item.Note))
            {
                document.Lines.Add(new ParsedLine { Text = $"  note: {item.Note}", Align = "left", Bold = false });
            }
        }

        var lines = items
            .Select(i => new BarTicketLine(
                string.IsNullOrWhiteSpace(i.Name) ? "(unnamed)" : i.Name,
                i.Qty.ToString("0.##", CultureInfo.InvariantCulture),
                i.Note))
            .ToList();

        return new BarTicketPreview(
            document,
            table ?? "N/A",
            waiter ?? "N/A",
            string.IsNullOrWhiteSpace(round) ? null : round,
            lines,
            items.Count,
            sourcePath);
    }

    private static List<NormalizedTicketItem> ResolveItems(JsonElement payload, out string sourcePath)
    {
        var candidates = new List<(string Path, JsonElement Array)>
        {
            ("items", TryGetArray(payload, "items")),
            ("ticket.items", TryGetNestedArray(payload, "ticket", "items")),
            ("products", TryGetArray(payload, "products")),
            ("lines", TryGetArray(payload, "lines"))
        };

        foreach (var candidate in candidates)
        {
            if (candidate.Array.ValueKind != JsonValueKind.Array || candidate.Array.GetArrayLength() == 0)
            {
                continue;
            }

            sourcePath = candidate.Path;
            return candidate.Array.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Object)
                .Select(NormalizeItem)
                .ToList();
        }

        sourcePath = "none";
        return new List<NormalizedTicketItem>();
    }

    private static JsonElement TryGetArray(JsonElement parent, string name)
    {
        if (parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
        {
            return value;
        }

        return default;
    }

    private static JsonElement TryGetNestedArray(JsonElement parent, string objectName, string arrayName)
    {
        if (!parent.TryGetProperty(objectName, out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        return TryGetArray(obj, arrayName);
    }

    private static NormalizedTicketItem NormalizeItem(JsonElement item)
    {
        var name = ReadString(item, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            name = ReadString(item, "artikl_name");
        }

        var note = ReadString(item, "note") ?? string.Empty;
        var qty = ReadQty(item);

        return new NormalizedTicketItem(name ?? string.Empty, qty, note);
    }

    private static string? ReadString(JsonElement item, string key)
    {
        if (!item.TryGetProperty(key, out var node))
        {
            return null;
        }

        return node.ValueKind switch
        {
            JsonValueKind.String => node.GetString(),
            JsonValueKind.Number => node.ToString(),
            _ => null
        };
    }

    private static decimal ReadQty(JsonElement item)
    {
        if (item.TryGetProperty("qty", out var qtyNode) && TryParseDecimalNode(qtyNode, out var qty))
        {
            return qty;
        }

        if (item.TryGetProperty("quantity", out var quantityNode) && TryParseDecimalNode(quantityNode, out var quantity))
        {
            return quantity;
        }

        return 0m;
    }

    private static bool TryParseDecimalNode(JsonElement node, out decimal value)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Number:
                return node.TryGetDecimal(out value);
            case JsonValueKind.String:
                var raw = node.GetString();
                return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                    || decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
            default:
                value = 0m;
                return false;
        }
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

    private static string NormalizeBase64(string input)
    {
        const string marker = "base64,";
        var idx = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return input[(idx + marker.Length)..];
        }

        return input.Trim();
    }

    private static bool TryDecodeBase64Pdf(string? raw, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(NormalizeBase64(raw));
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string SaveTempPdf(byte[] bytes, string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), "MozzartPrintHub", "pdf");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{prefix}-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private sealed record NormalizedTicketItem(string Name, decimal Qty, string Note);
    private sealed record BarTicketPreview(
        ParsedEscPosDocument Document,
        string Table,
        string Waiter,
        string? Round,
        IReadOnlyList<BarTicketLine> Lines,
        int ItemCount,
        string SourcePath);
}
