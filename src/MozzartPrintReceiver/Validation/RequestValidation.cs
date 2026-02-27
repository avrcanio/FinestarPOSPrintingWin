using System.Text.Json;
using MozzartPrintReceiver.Contracts;

namespace MozzartPrintReceiver.Validation;

public static class RequestValidation
{
    public static List<string> Validate(PrintJobRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.JobId))
        {
            errors.Add("job_id is required");
        }

        if (string.IsNullOrWhiteSpace(request.Kind))
        {
            errors.Add("kind is required");
        }
        else if (!string.Equals(request.Kind, PrintJobKind.ReceiptPdf, StringComparison.Ordinal) &&
                 !string.Equals(request.Kind, PrintJobKind.BarTicket, StringComparison.Ordinal))
        {
            errors.Add("kind must be one of: receipt_pdf, bar_ticket");
        }

        if (string.IsNullOrWhiteSpace(request.PrinterName))
        {
            errors.Add("printer_name is required");
        }

        if (request.Payload is null)
        {
            errors.Add("payload is required");
            return errors;
        }

        if (string.Equals(request.Kind, PrintJobKind.ReceiptPdf, StringComparison.Ordinal))
        {
            var payload = request.Payload.Deserialize<ReceiptPayload>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (payload is null || string.IsNullOrWhiteSpace(payload.PdfBase64))
            {
                errors.Add("payload.pdf_base64 is required for receipt_pdf");
            }
            else
            {
                try
                {
                    _ = Convert.FromBase64String(payload.PdfBase64);
                }
                catch (FormatException)
                {
                    errors.Add("payload.pdf_base64 is not valid base64");
                }
            }
        }

        if (string.Equals(request.Kind, PrintJobKind.BarTicket, StringComparison.Ordinal))
        {
            var payload = request.Payload.Deserialize<BarTicketPayload>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (payload is null)
            {
                errors.Add("payload is invalid for bar_ticket");
                return errors;
            }

            if (payload.Items.Count == 0)
            {
                errors.Add("payload.items must contain at least one item");
            }

            for (var i = 0; i < payload.Items.Count; i++)
            {
                var item = payload.Items[i];
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    errors.Add($"payload.items[{i}].name is required");
                }

                if (item.Qty <= 0)
                {
                    errors.Add($"payload.items[{i}].qty must be > 0");
                }
            }
        }

        return errors;
    }
}
