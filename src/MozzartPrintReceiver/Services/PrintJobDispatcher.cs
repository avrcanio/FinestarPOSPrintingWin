using System.Text.Json;
using MozzartPrintReceiver.Contracts;

namespace MozzartPrintReceiver.Services;

public sealed class PrintJobDispatcher : IPrintJobDispatcher
{
    private readonly IReceiptPrinter _receiptPrinter;
    private readonly IBarTicketPrinter _barTicketPrinter;

    public PrintJobDispatcher(IReceiptPrinter receiptPrinter, IBarTicketPrinter barTicketPrinter)
    {
        _receiptPrinter = receiptPrinter;
        _barTicketPrinter = barTicketPrinter;
    }

    public async Task<PrintDispatchResult> DispatchAsync(PrintJobRequest request, CancellationToken cancellationToken)
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        try
        {
            if (string.Equals(request.Kind, PrintJobKind.ReceiptPdf, StringComparison.Ordinal))
            {
                var payload = request.Payload.Deserialize<ReceiptPayload>(serializerOptions);
                if (payload is null)
                {
                    return new PrintDispatchResult(false, "invalid_payload", "payload is invalid for receipt_pdf");
                }

                await _receiptPrinter.PrintAsync(request.JobId, request.PrinterName, payload, cancellationToken);
                return new PrintDispatchResult(true);
            }

            if (string.Equals(request.Kind, PrintJobKind.BarTicket, StringComparison.Ordinal))
            {
                var payload = request.Payload.Deserialize<BarTicketPayload>(serializerOptions);
                if (payload is null)
                {
                    return new PrintDispatchResult(false, "invalid_payload", "payload is invalid for bar_ticket");
                }

                await _barTicketPrinter.PrintAsync(request.JobId, request.PrinterName, payload, cancellationToken);
                return new PrintDispatchResult(true);
            }

            return new PrintDispatchResult(false, "invalid_payload", "unsupported kind");
        }
        catch (OperationCanceledException)
        {
            return new PrintDispatchResult(false, "print_error", "print operation cancelled");
        }
        catch (Exception ex)
        {
            return new PrintDispatchResult(false, "print_error", ex.Message);
        }
    }
}
