using System.Text.Json.Serialization;

namespace MozzartPrintReceiver.Contracts;

public sealed class BarTicketPayload
{
    [JsonPropertyName("table")]
    public string? Table { get; set; }

    [JsonPropertyName("waiter")]
    public string? Waiter { get; set; }

    [JsonPropertyName("round_number")]
    public int? RoundNumber { get; set; }

    [JsonPropertyName("items")]
    public List<BarTicketItem> Items { get; set; } = new();
}

public sealed class BarTicketItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("qty")]
    public decimal Qty { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }
}
