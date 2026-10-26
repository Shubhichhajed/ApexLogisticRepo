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
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("/integration", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var allowedIps = configuration.GetSection("Security:AllowedIps").Get<string[]>() ??
            configuration.GetSection("AllowedIps").Get<string[]>() ??
            Array.Empty<string>();

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        var forwardedIps = context.Request.Headers["X-Forwarded-For"].ToString()
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var candidateIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            candidateIps.Add(remoteIp);
        }

        foreach (var forwardedIp in forwardedIps)
        {
            candidateIps.Add(forwardedIp);
        }

        var isAllowed = candidateIps.Any(ip => allowedIps.Contains(ip, StringComparer.OrdinalIgnoreCase));
        if (isAllowed)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { error = "IP address is not allowed for this endpoint." });
    }
}
