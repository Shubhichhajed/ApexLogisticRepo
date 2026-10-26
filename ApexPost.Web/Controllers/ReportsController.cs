using System.Security.Claims;
using ApexPost.Application;
using ApexPost.Domain;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class ReportsController : Controller
{
    private readonly ReportService reportService;

    public ReportsController(ReportService reportService)
    {
        this.reportService = reportService;
    }

    [HttpGet("/reports")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var branchCode = User.Claims.FirstOrDefault(c => c.Type == "BranchCode")?.Value;
        if (string.IsNullOrWhiteSpace(branchCode))
        {
            return Forbid();
        }

        var dailySales = await reportService.GetDailyBranchSalesAsync(BranchCode.Parse(branchCode), cancellationToken);
        var lowStock = await reportService.GetLowStockReportAsync(BranchCode.Parse(branchCode), 5, cancellationToken);
        var shipping = await reportService.GetShippingVolumeReportAsync(cancellationToken);

        var model = new ReportViewModel
        {
            Title = "Branch reports",
            Rows = dailySales.Concat(lowStock).Concat(shipping).ToList()
        };

        return View(model);
    }
}
