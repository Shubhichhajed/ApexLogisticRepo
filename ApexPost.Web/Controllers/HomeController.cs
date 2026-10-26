using System.Security.Claims;
using ApexPost.Application;
using ApexPost.Domain;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly DashboardService dashboardService;
    private readonly IProductRepository productRepository;
    private readonly IBranchRepository branchRepository;

    public HomeController(DashboardService dashboardService, IProductRepository productRepository, IBranchRepository branchRepository)
    {
        this.dashboardService = dashboardService;
        this.productRepository = productRepository;
        this.branchRepository = branchRepository;
    }

    [HttpGet("/")]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        return RedirectToAction("Login", "Auth");
    }

    [HttpGet("/home/dashboard")]
    [Authorize]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var branchCode = User.Claims.FirstOrDefault(c => c.Type == "BranchCode")?.Value;
        if (string.IsNullOrWhiteSpace(branchCode))
        {
            return Forbid();
        }

        var dashboard = await dashboardService.GetBranchDashboardAsync(BranchCode.Parse(branchCode), cancellationToken);
        var model = new DashboardViewModel
        {
            Branch = dashboard.Branch,
            LowStockProducts = dashboard.LowStockProducts,
            RecentTransactions = dashboard.RecentTransactions,
            Alerts = dashboard.Alerts.Select(item => item.Message).ToList()
        };

        return View(model);
    }

    [HttpGet("/products")]
    [Authorize]
    public async Task<IActionResult> Products(CancellationToken cancellationToken)
    {
        var products = await productRepository.GetAllAsync(cancellationToken);
        var branchCode = User.Claims.FirstOrDefault(c => c.Type == "BranchCode")?.Value;
        Branch? branch = null;
        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            branch = await branchRepository.GetByCodeAsync(BranchCode.Parse(branchCode), cancellationToken);
        }

        ViewBag.Branch = branch;
        return View(products);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}
