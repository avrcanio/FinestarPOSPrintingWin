using System.Diagnostics;
using Microsoft.Extensions.Options;
using MozzartPrintReceiver.Configuration;
using MozzartPrintReceiver.Contracts;
using MozzartPrintReceiver.Middleware;
using MozzartPrintReceiver.Observability;
using MozzartPrintReceiver.Services;
using MozzartPrintReceiver.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Services.Configure<ReceiverOptions>(builder.Configuration.GetSection(ReceiverOptions.SectionName));
builder.Services.Configure<PrintOptions>(builder.Configuration.GetSection(PrintOptions.SectionName));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IJobDeduplicator, MemoryJobDeduplicator>();
builder.Services.AddSingleton<TicketRenderer>();
builder.Services.AddSingleton<IReceiptPrinter, SumatraReceiptPrinter>();
builder.Services.AddSingleton<IBarTicketPrinter, PrintDocumentBarTicketPrinter>();
builder.Services.AddSingleton<IPrintJobDispatcher, PrintJobDispatcher>();

var receiverOptions = builder.Configuration.GetSection(ReceiverOptions.SectionName).Get<ReceiverOptions>() ?? new ReceiverOptions();
var bind = string.IsNullOrWhiteSpace(receiverOptions.Bind) ? "0.0.0.0" : receiverOptions.Bind;
var port = receiverOptions.Port <= 0 ? 8089 : receiverOptions.Port;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Parse(bind), port);
});

var app = builder.Build();

app.UseMiddleware<BridgeTokenMiddleware>();

app.MapPost("/print", async (
    PrintJobRequest request,
    IPrintJobDispatcher dispatcher,
    IJobDeduplicator deduplicator,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("PrintEndpoint");
    var started = Stopwatch.StartNew();

    var validationErrors = RequestValidation.Validate(request);
    if (validationErrors.Count > 0)
    {
        logger.LogWarning("Validation failed for job {JobId}: {Errors}", request.JobId, string.Join("; ", validationErrors));
        return Results.BadRequest(new ApiError("invalid_payload", "Request validation failed", validationErrors));
    }

    var duplicate = deduplicator.IsDuplicate(request.JobId);
    if (duplicate)
    {
        started.Stop();
        logger.LogPrintCompleted(request.JobId, request.Kind, request.PrinterName, started.ElapsedMilliseconds, "duplicate_suppressed", true);
        return Results.Ok(new { status = "duplicate_suppressed", job_id = request.JobId });
    }

    var result = await dispatcher.DispatchAsync(request, cancellationToken);

    started.Stop();
    if (!result.Success)
    {
        var errorCode = result.ErrorCode ?? "print_error";
        var errorMessage = result.ErrorMessage ?? "Print job failed";

        logger.LogPrintFailed(request.JobId, request.Kind, request.PrinterName, started.ElapsedMilliseconds, errorCode, errorMessage);

        return errorCode switch
        {
            "invalid_payload" => Results.BadRequest(new ApiError(errorCode, errorMessage)),
            _ => Results.Problem(statusCode: 500, title: "print_error", detail: errorMessage)
        };
    }

    deduplicator.Remember(request.JobId);

    logger.LogPrintCompleted(request.JobId, request.Kind, request.PrinterName, started.ElapsedMilliseconds, "printed", false);
    return Results.Ok(new { status = "printed", job_id = request.JobId });
});

app.Run();
