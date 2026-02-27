namespace MozzartPrintHub.WinForms.Domain;

public sealed class ParsedEscPosDocument
{
    public List<ParsedLine> Lines { get; } = new();
}

public sealed class ParsedLine
{
    public required string Text { get; init; }
    public required string Align { get; init; }
    public required bool Bold { get; init; }
    public bool IsCutMarker { get; init; }
}
