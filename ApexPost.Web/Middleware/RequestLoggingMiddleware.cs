using System.Diagnostics;

namespace ApexPost.Web.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<RequestLoggingMiddleware> logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            logger.LogInformation("Request started {Method} {Path} with correlation {CorrelationId}", context.Request.Method, context.Request.Path, context.TraceIdentifier);
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation("Request completed {Method} {Path} in {DurationMs} ms with status {StatusCode}", context.Request.Method, context.Request.Path, stopwatch.ElapsedMilliseconds, context.Response.StatusCode);
        }
    }
}
