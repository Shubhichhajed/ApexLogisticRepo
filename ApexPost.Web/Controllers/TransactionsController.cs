using System.Security.Claims;
using ApexPost.Application;
using ApexPost.Domain;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
public sealed class TransactionsController : Controller
{
    private readonly TransactionService transactionService;
    private readonly ITransactionRepository transactionRepository;

    public TransactionsController(TransactionService transactionService, ITransactionRepository transactionRepository)
    {
        this.transactionService = transactionService;
        this.transactionRepository = transactionRepository;
    }

    [HttpGet("/transactions/create")]
    public IActionResult Create()
    {
        var branchCode = User.Claims.FirstOrDefault(c => c.Type == "BranchCode")?.Value ?? "BR-001";
        return View(new TransactionCreateViewModel
        {
            BranchCode = branchCode,
            ClerkId = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value ?? string.Empty,
            CounterId = "C-01"
        });
    }

    [HttpPost("/transactions/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransactionCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var branchCode = BranchCode.Parse(model.BranchCode);
            var clerkId = User.Claims.FirstOrDefault(c => c.Type == "StaffId")?.Value ?? model.ClerkId;
            var transaction = await transactionService.CreateOpenTransactionAsync(branchCode, model.CounterId, clerkId, Enum.Parse<PaymentMethod>(model.PaymentMethod, ignoreCase: true), cancellationToken);
            var line = new TransactionLine(
                Guid.NewGuid().ToString("N"),
                model.Description,
                model.LineType,
                model.Quantity,
                new Money(model.UnitPrice),
                model.TaxAmount,
                model.LineType == "Shipping" ? ServiceLevel.Standard.ToString() : null,
                model.LineType == "Shipping" ? 1.2m : null);

            var updated = await transactionService.AddLineAsync(transaction.TransactionId, line, cancellationToken);
            var completed = await transactionService.CompleteTransactionAsync(updated.TransactionId, cancellationToken);
            return RedirectToAction(nameof(Summary), new { id = completed.TransactionId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet("/transactions/summary/{id}")]
    public async Task<IActionResult> Summary(string id, CancellationToken cancellationToken)
    {
        var transaction = await transactionRepository.GetByIdAsync(id, cancellationToken);
        if (transaction is null)
        {
            return NotFound();
        }

        return View(transaction);
    }
}
