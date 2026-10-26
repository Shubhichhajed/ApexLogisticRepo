namespace ApexPost.Web.Middleware;

public sealed class MaintenanceModeMiddleware
{
    private readonly RequestDelegate next;
    private readonly IConfiguration configuration;

    public MaintenanceModeMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        this.next = next;
        this.configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isMaintenanceMode = configuration.GetValue<bool>("Application:MaintenanceMode", false);
        if (isMaintenanceMode && !context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "120";
            await context.Response.WriteAsJsonAsync(new { error = "Maintenance mode is active. Please try again later." });
            return;
        }

        await next(context);
    }
}
