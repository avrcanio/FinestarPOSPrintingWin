using System.Diagnostics;

namespace MozzartPrintHub.WinForms.Services;

public sealed class PdfPrintService
{
    public bool TryPrint(string pdfPath, string printerName, string sumatraPath, int timeoutSeconds, out string? error)
    {
        error = null;

        if (!File.Exists(pdfPath))
        {
            error = $"PDF file not found: {pdfPath}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sumatraPath) || !File.Exists(sumatraPath))
        {
            error = $"SumatraPDF not found: {sumatraPath}";
            return false;
        }

        try
        {
            var args = $"-silent -print-to \"{printerName}\" \"{pdfPath}\"";
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = sumatraPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                error = "Failed to start SumatraPDF process.";
                return false;
            }

            if (!process.WaitForExit(Math.Max(1, timeoutSeconds) * 1000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                error = $"SumatraPDF print timeout after {timeoutSeconds}s.";
                return false;
            }

            if (process.ExitCode != 0)
            {
                error = $"SumatraPDF exit code: {process.ExitCode}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
