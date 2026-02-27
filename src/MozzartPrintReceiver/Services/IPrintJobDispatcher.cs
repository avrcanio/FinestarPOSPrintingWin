using MozzartPrintReceiver.Contracts;

namespace MozzartPrintReceiver.Services;

public interface IPrintJobDispatcher
{
    Task<PrintDispatchResult> DispatchAsync(PrintJobRequest request, CancellationToken cancellationToken);
}

public sealed record PrintDispatchResult(bool Success, string? ErrorCode = null, string? ErrorMessage = null);
