using System.ComponentModel.DataAnnotations;
using ApexPost.Application;
using ApexPost.Domain;

namespace ApexPost.Web.Models;

public sealed class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public sealed class DashboardViewModel
{
    public Branch Branch { get; set; } = default!;
    public IReadOnlyCollection<Product> LowStockProducts { get; set; } = Array.Empty<Product>();
    public IReadOnlyCollection<CounterTransaction> RecentTransactions { get; set; } = Array.Empty<CounterTransaction>();
    public IReadOnlyCollection<string> Alerts { get; set; } = Array.Empty<string>();
}

public sealed class TransactionCreateViewModel
{
    [Required]
    public string BranchCode { get; set; } = string.Empty;
    [Required]
    public string CounterId { get; set; } = string.Empty;
    [Required]
    public string ClerkId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LineType { get; set; } = "Retail";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal TaxAmount { get; set; } = 0m;
    public string PaymentMethod { get; set; } = "Card";
}

public sealed class ReportViewModel
{
    public string Title { get; set; } = string.Empty;
    public IReadOnlyCollection<ReportSummary> Rows { get; set; } = Array.Empty<ReportSummary>();
}

public sealed record ParcelStatusResponse(string TrackingNumber, string Status, string Courier, string Zone);
