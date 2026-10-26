using System.Net;

namespace ApexPost.Web.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ExceptionHandlingMiddleware> logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            logger.LogError(ex, "Unhandled exception for request {TraceId} on {Path}", traceId, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var payload = new
            {
                title = "An unexpected error occurred.",
                status = (int)HttpStatusCode.InternalServerError,
                detail = "A safe error message has been returned. Please contact support with the trace identifier.",
                traceId
            };

            await context.Response.WriteAsJsonAsync(payload);
        }
    }
}
