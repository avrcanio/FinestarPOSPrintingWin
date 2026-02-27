namespace MozzartPrintReceiver.Observability;

public static partial class LoggingExtensions
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Print finished job_id={JobId} kind={Kind} printer_name={PrinterName} duration_ms={DurationMs} status={Status} duplicate={Duplicate}")]
    public static partial void LogPrintCompleted(
        this ILogger logger,
        string jobId,
        string kind,
        string printerName,
        long durationMs,
        string status,
        bool duplicate);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "Print failed job_id={JobId} kind={Kind} printer_name={PrinterName} duration_ms={DurationMs} error_code={ErrorCode} error={ErrorMessage}")]
    public static partial void LogPrintFailed(
        this ILogger logger,
        string jobId,
        string kind,
        string printerName,
        long durationMs,
        string errorCode,
        string errorMessage);
}
