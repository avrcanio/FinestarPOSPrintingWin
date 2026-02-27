using MozzartPrintReceiver.Contracts;

namespace MozzartPrintReceiver.Services;

public interface IReceiptPrinter
{
    Task PrintAsync(string jobId, string printerName, ReceiptPayload payload, CancellationToken cancellationToken);
}
