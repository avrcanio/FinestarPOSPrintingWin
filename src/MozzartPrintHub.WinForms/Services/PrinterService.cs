using System.Drawing;
using System.Drawing.Printing;
using MozzartPrintHub.WinForms.Domain;

namespace MozzartPrintHub.WinForms.Services;

public sealed class PrinterService
{
    public bool TryPrint(ParsedEscPosDocument document, string printerName, out string? error)
    {
        error = null;
        try
        {
            using var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = printerName;
            if (!printDoc.PrinterSettings.IsValid)
            {
                error = $"Printer '{printerName}' is not available.";
                return false;
            }

            var lines = document.Lines.ToList();
            printDoc.PrintPage += (_, e) =>
            {
                var graphics = e.Graphics;
                if (graphics is null)
                {
                    e.HasMorePages = false;
                    return;
                }

                using var fontNormal = new Font("Consolas", 9, FontStyle.Regular);
                using var fontBold = new Font("Consolas", 9, FontStyle.Bold);

                var y = e.MarginBounds.Top;
                foreach (var line in lines)
                {
                    var font = line.Bold ? fontBold : fontNormal;
                    var width = graphics.MeasureString(line.Text, font).Width;
                    var x = line.Align switch
                    {
                        "center" => e.MarginBounds.Left + ((e.MarginBounds.Width - width) / 2),
                        "right" => e.MarginBounds.Right - width,
                        _ => e.MarginBounds.Left
                    };

                    graphics.DrawString(line.Text, font, Brushes.Black, x, y);
                    y += (int)font.GetHeight(graphics) + 2;
                }

                e.HasMorePages = false;
            };

            printDoc.Print();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
