using System.Text;
using MozzartPrintHub.WinForms.Domain;
using UglyToad.PdfPig;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Docnet.Core;
using Docnet.Core.Models;

namespace MozzartPrintHub.WinForms.Services;

public sealed class ReceiptPreviewService
{
    public ReceiptPreviewResult BuildFromPdfBase64(string? pdfBase64)
    {
        if (string.IsNullOrWhiteSpace(pdfBase64))
        {
            var document = new ParsedEscPosDocument();
            document.Lines.Add(new ParsedLine { Text = "[PDF payload is empty]", Align = "left", Bold = true });
            return new ReceiptPreviewResult(document, null);
        }

        try
        {
            var clean = NormalizeBase64(pdfBase64);
            var bytes = Convert.FromBase64String(clean);
            return BuildFromPdfBytes(bytes);
        }
        catch (Exception ex)
        {
            var document = new ParsedEscPosDocument();
            document.Lines.Add(new ParsedLine { Text = "[Failed to decode receipt PDF]", Align = "left", Bold = true });
            document.Lines.Add(new ParsedLine { Text = ex.Message, Align = "left", Bold = false });
            return new ReceiptPreviewResult(document, null);
        }
    }

    public ReceiptPreviewResult BuildFromPdfBytes(byte[] bytes)
    {
        var document = new ParsedEscPosDocument();
        string? previewImagePath = null;

        try
        {
            using var stream = new MemoryStream(bytes);
            using var pdf = PdfDocument.Open(stream);

            document.Lines.Add(new ParsedLine { Text = "RECEIPT PREVIEW", Align = "center", Bold = true });
            document.Lines.Add(new ParsedLine { Text = $"Pages: {pdf.NumberOfPages}", Align = "left", Bold = false });
            document.Lines.Add(new ParsedLine { Text = new string('-', 42), Align = "left", Bold = false });

            if (pdf.NumberOfPages == 0)
            {
                document.Lines.Add(new ParsedLine { Text = "[PDF has no pages]", Align = "left", Bold = false });
                return new ReceiptPreviewResult(document, null);
            }

            var firstPage = pdf.GetPage(1);
            var text = firstPage.Text ?? string.Empty;
            previewImagePath = TryRenderPdfPreview(bytes);
            if (string.IsNullOrWhiteSpace(text))
            {
                document.Lines.Add(new ParsedLine { Text = "[No text extracted from first page]", Align = "left", Bold = false });
                return new ReceiptPreviewResult(document, previewImagePath);
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

        return new ReceiptPreviewResult(document, previewImagePath);
    }

    private static string? TryRenderPdfPreview(byte[] pdfBytes)
    {
        try
        {
            using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(2.0));
            using var pageReader = docReader.GetPageReader(0);
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();
            var rawBytes = pageReader.GetImage();
            if (rawBytes is null || rawBytes.Length == 0 || width <= 0 || height <= 0)
            {
                return null;
            }

            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, width, height);
            var bitmapData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
            try
            {
                Marshal.Copy(rawBytes, 0, bitmapData.Scan0, Math.Min(rawBytes.Length, Math.Abs(bitmapData.Stride) * height));
            }
            finally
            {
                bmp.UnlockBits(bitmapData);
            }

            var fileName = $"receipt-preview-{Guid.NewGuid():N}.png";
            var path = Path.Combine(Path.GetTempPath(), fileName);
            bmp.Save(path, ImageFormat.Png);
            return path;
        }
        catch
        {
            return null;
        }
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

public sealed record ReceiptPreviewResult(ParsedEscPosDocument Document, string? PreviewImagePath);
