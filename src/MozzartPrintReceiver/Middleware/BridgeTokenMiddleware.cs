using Microsoft.Extensions.Options;
using MozzartPrintReceiver.Configuration;
using MozzartPrintReceiver.Contracts;

namespace MozzartPrintReceiver.Middleware;

public sealed class BridgeTokenMiddleware
{
    private const string HeaderName = "X-Bridge-Token";

    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<ReceiverOptions> _options;

    public BridgeTokenMiddleware(RequestDelegate next, IOptionsMonitor<ReceiverOptions> options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.Path.Equals("/print", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var token = _options.CurrentValue.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedValue))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new ApiError("missing_token", "X-Bridge-Token header is required"));
            return;
        }

        if (!string.Equals(providedValue.ToString(), token, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ApiError("invalid_token", "X-Bridge-Token is invalid"));
            return;
        }

        await _next(context);
    }
}
