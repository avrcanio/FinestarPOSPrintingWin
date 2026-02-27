using System.Drawing;
using System.Drawing.Printing;
using MozzartPrintReceiver.Contracts;

namespace MozzartPrintReceiver.Services;

public sealed class PrintDocumentBarTicketPrinter : IBarTicketPrinter
{
    private readonly TicketRenderer _renderer;

    public PrintDocumentBarTicketPrinter(TicketRenderer renderer)
    {
        _renderer = renderer;
    }

    public Task PrintAsync(string jobId, string printerName, BarTicketPayload payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var content = _renderer.Render(payload);
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var currentLine = 0;

        using var printDocument = new PrintDocument();
        printDocument.PrinterSettings.PrinterName = printerName;

        if (!printDocument.PrinterSettings.IsValid)
        {
            throw new InvalidOperationException($"Printer '{printerName}' was not found or is not valid");
        }

        using var font = new Font(FontFamily.GenericMonospace, 9f, FontStyle.Regular);

        printDocument.PrintPage += (_, args) =>
        {
            var graphics = args.Graphics ?? throw new InvalidOperationException("Graphics context is unavailable for print page rendering");
            var lineHeight = font.GetHeight(graphics);
            float y = args.MarginBounds.Top;

            while (currentLine < lines.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (y + lineHeight > args.MarginBounds.Bottom)
                {
                    args.HasMorePages = true;
                    return;
                }

                graphics.DrawString(lines[currentLine], font, Brushes.Black, args.MarginBounds.Left, y);
                currentLine++;
                y += lineHeight;
            }

            args.HasMorePages = false;
        };

        printDocument.Print();
        return Task.CompletedTask;
    }
}
