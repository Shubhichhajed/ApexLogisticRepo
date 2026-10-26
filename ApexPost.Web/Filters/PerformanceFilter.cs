using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class PerformanceFilter : IAsyncActionFilter
{
    private readonly ILogger<PerformanceFilter> logger;
    private readonly IConfiguration configuration;

    public PerformanceFilter(ILogger<PerformanceFilter> logger, IConfiguration configuration)
    {
        this.logger = logger;
        this.configuration = configuration;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await next();
        stopwatch.Stop();

        var threshold = configuration.GetValue<int>("Performance:SlowActionThresholdMs", 500);
        if (stopwatch.ElapsedMilliseconds > threshold)
        {
            logger.LogWarning("Slow action {ActionName} took {Elapsed} ms", context.ActionDescriptor.DisplayName, stopwatch.ElapsedMilliseconds);
        }

        var statusCode = result.Result switch
        {
            StatusCodeResult statusResult => statusResult.StatusCode,
            ObjectResult objectResult => objectResult.StatusCode ?? 200,
            _ => 200
        };
        if (statusCode >= 500)
        {
            logger.LogError("Action {ActionName} completed with status {StatusCode}", context.ActionDescriptor.DisplayName, statusCode);
        }
    }
}
