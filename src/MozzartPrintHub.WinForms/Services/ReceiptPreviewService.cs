using System.Text;
using MozzartPrintHub.WinForms.Domain;
using UglyToad.PdfPig;

namespace MozzartPrintHub.WinForms.Services;

public sealed class ReceiptPreviewService
{
    public ParsedEscPosDocument BuildFromPdfBase64(string? pdfBase64)
    {
        var document = new ParsedEscPosDocument();

        if (string.IsNullOrWhiteSpace(pdfBase64))
        {
            document.Lines.Add(new ParsedLine { Text = "[PDF payload is empty]", Align = "left", Bold = true });
            return document;
        }

        try
        {
            var clean = NormalizeBase64(pdfBase64);
            var bytes = Convert.FromBase64String(clean);

            using var stream = new MemoryStream(bytes);
            using var pdf = PdfDocument.Open(stream);

            document.Lines.Add(new ParsedLine { Text = "RECEIPT PREVIEW", Align = "center", Bold = true });
            document.Lines.Add(new ParsedLine { Text = $"Pages: {pdf.NumberOfPages}", Align = "left", Bold = false });
            document.Lines.Add(new ParsedLine { Text = new string('-', 42), Align = "left", Bold = false });

            if (pdf.NumberOfPages == 0)
            {
                document.Lines.Add(new ParsedLine { Text = "[PDF has no pages]", Align = "left", Bold = false });
                return document;
            }

            var firstPage = pdf.GetPage(1);
            var text = firstPage.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                document.Lines.Add(new ParsedLine { Text = "[No text extracted from first page]", Align = "left", Bold = false });
                return document;
            }

            foreach (var line in SplitLines(text).Take(80))
            {
                document.Lines.Add(new ParsedLine { Text = line, Align = "left", Bold = false });
            }
        }
        catch (Exception ex)
        {
            document.Lines.Clear();
            document.Lines.Add(new ParsedLine { Text = "[Failed to parse receipt PDF]", Align = "left", Bold = true });
            document.Lines.Add(new ParsedLine { Text = ex.Message, Align = "left", Bold = false });
        }

        return document;
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

    private static IEnumerable<string> SplitLines(string text)
    {
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch == '\r')
            {
                continue;
            }

            if (ch == '\n')
            {
                var line = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    yield return line;
                }

                sb.Clear();
                continue;
            }

            sb.Append(ch);
        }

        if (sb.Length > 0)
        {
            var line = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }
}
