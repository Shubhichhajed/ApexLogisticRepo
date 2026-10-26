namespace ApexPost.Web.Middleware;

public sealed class IpRestrictionMiddleware
{
    private readonly RequestDelegate next;
    private readonly IConfiguration configuration;

    public IpRestrictionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        this.next = next;
        this.configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var allowedIps = configuration.GetSection("AllowedIps").Get<string[]>() ?? Array.Empty<string>();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/integration", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(remoteIp) && allowedIps.Contains(remoteIp, StringComparer.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "IP address is not allowed for this endpoint." });
            return;
        }

        await next(context);
    }
}
