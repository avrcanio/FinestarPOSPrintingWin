using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MozzartPrintReceiver.Contracts;

public sealed class PrintJobRequest
{
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("printer_name")]
    public string PrinterName { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public JsonObject Payload { get; set; } = new();

    [JsonPropertyName("meta")]
    public JsonObject? Meta { get; set; }
}
