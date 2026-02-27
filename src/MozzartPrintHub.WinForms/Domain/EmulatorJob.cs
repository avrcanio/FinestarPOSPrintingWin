namespace MozzartPrintHub.WinForms.Domain;

public sealed class EmulatorJob
{
    public required string Id { get; init; }
    public required DateTime ReceivedAtUtc { get; init; }
    public required string Source { get; init; }
    public required string ClientIp { get; init; }
    public required int RawSizeBytes { get; init; }
    public int UnknownCommandCount { get; init; }
    public required ParsedEscPosDocument Document { get; init; }
    public string? PrintStatus { get; set; }
    public string? Error { get; set; }
}
