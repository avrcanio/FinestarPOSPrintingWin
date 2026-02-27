using MozzartPrintReceiver.Contracts;

namespace MozzartPrintReceiver.Services;

public interface IBarTicketPrinter
{
    Task PrintAsync(string jobId, string printerName, BarTicketPayload payload, CancellationToken cancellationToken);
}
