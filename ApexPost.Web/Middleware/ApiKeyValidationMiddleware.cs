using ApexPost.Web.Attributes;

namespace ApexPost.Web.Middleware;

public sealed class ApiKeyValidationMiddleware
{
    private readonly RequestDelegate next;
    private readonly IConfiguration configuration;

    public ApiKeyValidationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        this.next = next;
        this.configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var requireApiKey = endpoint?.Metadata.GetMetadata<RequireApiKeyAttribute>() is not null;
        if (!requireApiKey)
        {
            await next(context);
            return;
        }

        var configuredKey = configuration["Integration:ApiKey"] ?? configuration["ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Integration API key is not configured." });
            return;
        }

        var suppliedKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(suppliedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing API key." });
            return;
        }

        var constantTimeEquals = string.Equals(suppliedKey, configuredKey, StringComparison.Ordinal);
        if (!constantTimeEquals)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
            return;
        }

        await next(context);
    }
}
