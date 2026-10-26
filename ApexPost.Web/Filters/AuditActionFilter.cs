using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using ApexPost.Application;
using ApexPost.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

public sealed class AuditActionFilter : IAsyncActionFilter
{
    private readonly IAuditStore auditStore;
    private readonly ILogger<AuditActionFilter> logger;

    public AuditActionFilter(IAuditStore auditStore, ILogger<AuditActionFilter> logger)
    {
        this.auditStore = auditStore;
        this.logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        var action = context.ActionDescriptor.DisplayName ?? "unknown";
        var staffId = context.HttpContext.User.FindFirst("StaffId")?.Value ?? "anonymous";
        var role = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value ?? context.HttpContext.User.FindFirst("Role")?.Value ?? "anonymous";
        var branchCode = context.HttpContext.User.FindFirst("BranchCode")?.Value ?? "unknown";

        var result = await next();
        stopwatch.Stop();

        var routeValues = JsonSerializer.Serialize(context.RouteData.Values.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString() ?? string.Empty));
        var statusCode = result.Result switch
        {
            StatusCodeResult statusResult => statusResult.StatusCode,
            ObjectResult objectResult => objectResult.StatusCode ?? 200,
            _ => 200
        };
        var correlationId = context.HttpContext.TraceIdentifier;

        var auditEntry = new AuditEntry(
            Guid.NewGuid().ToString("N"),
            staffId,
            role,
            branchCode,
            context.Controller.GetType().Name,
            action,
            routeValues,
            statusCode,
            stopwatch.Elapsed.TotalMilliseconds,
            correlationId,
            "ActionExecuted",
            JsonSerializer.Serialize(new { controller = context.Controller.GetType().Name, action, route = context.HttpContext.Request.Path.Value }),
            DateTime.UtcNow);

        await auditStore.AppendAsync(auditEntry, context.HttpContext.RequestAborted);
        logger.LogInformation("Audit recorded {@AuditEntry}", auditEntry);
    }
}
