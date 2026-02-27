using System.Diagnostics;
using Microsoft.Extensions.Options;
using MozzartPrintReceiver.Configuration;
using MozzartPrintReceiver.Contracts;

namespace MozzartPrintReceiver.Services;

public sealed class SumatraReceiptPrinter : IReceiptPrinter
{
    private readonly PrintOptions _options;

    public SumatraReceiptPrinter(IOptions<PrintOptions> options)
    {
        _options = options.Value;
    }

    public async Task PrintAsync(string jobId, string printerName, ReceiptPayload payload, CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.SumatraPath))
        {
            throw new InvalidOperationException($"SumatraPDF executable not found at '{_options.SumatraPath}'");
        }

        Directory.CreateDirectory(_options.TempDir);

        var fileName = $"{SanitizeFileComponent(jobId)}-{Guid.NewGuid():N}.pdf";
        var pdfPath = Path.Combine(_options.TempDir, fileName);

        try
        {
            var bytes = Convert.FromBase64String(payload.PdfBase64);
            await File.WriteAllBytesAsync(pdfPath, bytes, cancellationToken);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.SumatraPath,
                    Arguments = $"-silent -print-to \"{printerName}\" \"{pdfPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start SumatraPDF process");
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.SumatraTimeoutSeconds)));

            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"SumatraPDF exited with code {process.ExitCode}");
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"SumatraPDF print timed out after {_options.SumatraTimeoutSeconds} seconds");
        }
        finally
        {
            TryDelete(pdfPath);
        }
    }

    private static string SanitizeFileComponent(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    private static void TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}
