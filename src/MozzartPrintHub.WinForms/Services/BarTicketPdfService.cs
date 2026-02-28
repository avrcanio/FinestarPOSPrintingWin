using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MozzartPrintHub.WinForms.Services;

public sealed class BarTicketPdfService
{
    public byte[] BuildPdf(string table, string waiter, string? round, IReadOnlyList<BarTicketLine> lines)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11));

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Text("BAR TICKET").SemiBold().FontSize(16);
                    column.Item().Text($"Table: {table} | Waiter: {waiter}");
                    if (!string.IsNullOrWhiteSpace(round))
                    {
                        column.Item().Text($"Round: {round}");
                    }

                    column.Item().LineHorizontal(1);

                    foreach (var line in lines)
                    {
                        column.Item().Text($"{line.Qty} x {line.Name}");
                        if (!string.IsNullOrWhiteSpace(line.Note))
                        {
                            column.Item().Text($"  note: {line.Note}");
                        }
                    }
                });
            });
        }).GeneratePdf();
    }
}

public sealed record BarTicketLine(string Name, string Qty, string Note);
