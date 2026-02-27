using System.Text.Json.Serialization;

namespace MozzartPrintReceiver.Contracts;

public sealed class ReceiptPayload
{
    [JsonPropertyName("pdf_base64")]
    public string PdfBase64 { get; set; } = string.Empty;
}
