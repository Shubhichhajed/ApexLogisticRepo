using ApexPost.Domain;

namespace ApexPost.Application;

public interface IBranchRepository : IRepository<Branch>
{
    Task<Branch?> GetByCodeAsync(BranchCode code, CancellationToken cancellationToken = default);
}

public interface IStaffRepository : IRepository<Staff>
{
    Task<Staff?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Staff>> GetByBranchAsync(BranchCode branchCode, CancellationToken cancellationToken = default);
}

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default);
}

public interface ITransactionRepository : IRepository<CounterTransaction>
{
    Task<IReadOnlyCollection<CounterTransaction>> GetByBranchAsync(BranchCode branchCode, CancellationToken cancellationToken = default);
}

public interface IAuditStore
{
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AuditEntry>> GetAllAsync(CancellationToken cancellationToken = default);
}

public sealed record BranchDashboard(
    Branch Branch,
    IReadOnlyCollection<Product> LowStockProducts,
    IReadOnlyCollection<CounterTransaction> RecentTransactions,
    IReadOnlyCollection<AlertMessage> Alerts)
{
    public Branch Branch { get; init; } = Branch;
    public IReadOnlyCollection<Product> LowStockProducts { get; init; } = LowStockProducts;
    public IReadOnlyCollection<CounterTransaction> RecentTransactions { get; init; } = RecentTransactions;
    public IReadOnlyCollection<AlertMessage> Alerts { get; init; } = Alerts;
}

public sealed record AlertMessage(string Message, string Kind)
{
    public string Message { get; init; } = Message;
    public string Kind { get; init; } = Kind;
}

public sealed record ReportSummary(
    string Title,
    decimal Value,
    string Grouping)
{
    public string Title { get; init; } = Title;
    public decimal Value { get; init; } = Value;
    public string Grouping { get; init; } = Grouping;
}

public sealed class PricingService
{
    public Money CalculateRetailTotal(Product product, int quantity)
    {
        var subtotal = product.UnitPrice.Multiply(quantity);
        var taxRate = product.TaxCategory switch
        {
            TaxCategory.Standard => 0.2m,
            TaxCategory.Reduced => 0.1m,
            TaxCategory.Exempt => 0m,
            _ => 0m
        };

        var taxAmount = subtotal.TaxedValue(taxRate);
        return subtotal.Add(new Money(taxAmount));
    }

    public decimal CalculateShippingFee(ShippingZone zone, Weight weight, ServiceLevel level)
    {
        return ApexPostRules.CalculateShippingFee(zone, weight, level);
    }
}

public sealed class AuthenticationService
{
    private readonly IStaffRepository staffRepository;

    public AuthenticationService(IStaffRepository staffRepository)
    {
        this.staffRepository = staffRepository;
    }

    public async Task<Staff?> AuthenticateAsync(string email, CancellationToken cancellationToken = default)
    {
        var staff = await staffRepository.GetByEmailAsync(email, cancellationToken);
        return staff is { IsActive: true } ? staff : null;
    }
}

public sealed class InventoryService
{
    private readonly IProductRepository productRepository;
    private readonly IBranchRepository branchRepository;

    public InventoryService(IProductRepository productRepository, IBranchRepository branchRepository)
    {
        this.productRepository = productRepository;
        this.branchRepository = branchRepository;
    }

    public async Task<Branch?> ReserveStockAsync(BranchCode branchCode, Sku sku, int requestedQuantity, CancellationToken cancellationToken = default)
    {
        var branch = await branchRepository.GetByCodeAsync(branchCode, cancellationToken);
        if (branch is null)
        {
            return null;
        }

        if (!ApexPostRules.HasSufficientStock(branch, sku, requestedQuantity))
        {
            throw new InvalidOperationException("Insufficient stock available.");
        }

        branch.AdjustStock(sku, -requestedQuantity);
        await branchRepository.UpdateAsync(branch, cancellationToken);
        return branch;
    }

    public async Task<IReadOnlyCollection<Product>> GetLowStockProductsAsync(BranchCode branchCode, int threshold, CancellationToken cancellationToken = default)
    {
        var branch = await branchRepository.GetByCodeAsync(branchCode, cancellationToken);
        if (branch is null)
        {
            return Array.Empty<Product>();
        }

        var products = await productRepository.GetAllAsync(cancellationToken);
        var lowStock = products
            .Where(product => branch.GetStock(product.Sku) <= threshold)
            .ToList();

        return lowStock;
    }
}

public sealed class TransactionService
{
    private readonly ITransactionRepository transactionRepository;
    private readonly IBranchRepository branchRepository;
    private readonly IStaffRepository staffRepository;
    private readonly IProductRepository productRepository;
    private readonly InventoryService inventoryService;

    public event EventHandler<StockLowEvent>? StockLow;
    public event EventHandler<TransactionCompletedEvent>? TransactionCompleted;
    public event EventHandler<TransactionVoidedEvent>? TransactionVoided;
    public event EventHandler<MoneyOrderIssuedEvent>? MoneyOrderIssued;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IBranchRepository branchRepository,
        IStaffRepository staffRepository,
        IProductRepository productRepository,
        InventoryService inventoryService)
    {
        this.transactionRepository = transactionRepository;
        this.branchRepository = branchRepository;
        this.staffRepository = staffRepository;
        this.productRepository = productRepository;
        this.inventoryService = inventoryService;
    }

    public async Task<CounterTransaction> CreateOpenTransactionAsync(BranchCode branchCode, string counterId, string clerkId, PaymentMethod paymentMethod, CancellationToken cancellationToken = default)
    {
        var branch = await branchRepository.GetByCodeAsync(branchCode, cancellationToken);
        if (branch is null || !ApexPostRules.IsBranchOpen(branch))
        {
            throw new InvalidOperationException("The branch is closed or unavailable.");
        }

        var staff = await staffRepository.GetByIdAsync(clerkId, cancellationToken);
        if (staff is null || !ApexPostRules.IsStaffAllowedToProcess(staff, branch))
        {
            throw new InvalidOperationException("The staff member cannot process transactions at this branch.");
        }

        var transaction = new CounterTransaction(
            Guid.NewGuid().ToString("N"),
            branchCode,
            counterId,
            clerkId,
            new List<TransactionLine>(),
            paymentMethod,
            TransactionStatus.Open,
            DateTime.UtcNow,
            Guid.NewGuid().ToString("N"));

        await transactionRepository.AddAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task<CounterTransaction> AddLineAsync(string transactionId, TransactionLine line, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null)
        {
            throw new InvalidOperationException("Transaction was not found.");
        }

        var items = transaction.Lines.ToList();
        items.Add(line);

        var updated = transaction with { Lines = items };
        await transactionRepository.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    public async Task<CounterTransaction> CompleteTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null)
        {
            throw new InvalidOperationException("Transaction was not found.");
        }

        foreach (var line in transaction.Lines)
        {
            if (line.LineType == "Retail")
            {
                var product = await productRepository.GetBySkuAsync(Sku.Parse(line.Description), cancellationToken);
                if (product is null)
                {
                    throw new InvalidOperationException($"Product {line.Description} was not found.");
                }

                var branch = await branchRepository.GetByCodeAsync(transaction.BranchCode, cancellationToken);
                if (branch is null)
                {
                    throw new InvalidOperationException("Branch not found.");
                }

                if (!ApexPostRules.HasSufficientStock(branch, product.Sku, line.Quantity))
                {
                    throw new InvalidOperationException($"Insufficient stock for SKU {product.Sku}.");
                }

                branch.AdjustStock(product.Sku, -line.Quantity);
                await branchRepository.UpdateAsync(branch, cancellationToken);

                if (branch.GetStock(product.Sku) <= 5)
                {
                    StockLow?.Invoke(this, new StockLowEvent(branch.Code, product.Sku, branch.GetStock(product.Sku)));
                }
            }
        }

        var completed = transaction with { Status = TransactionStatus.Paid };
        await transactionRepository.UpdateAsync(completed, cancellationToken);
        TransactionCompleted?.Invoke(this, new TransactionCompletedEvent(completed.TransactionId, completed.GrandTotal.Amount));
        return completed;
    }

    public async Task<CounterTransaction> VoidAsync(string transactionId, string actorStaffId, string reason, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null)
        {
            throw new InvalidOperationException("Transaction was not found.");
        }

        var staff = await staffRepository.GetByIdAsync(actorStaffId, cancellationToken);
        if (staff is null || staff.Role != StaffRole.BranchManager)
        {
            throw new InvalidOperationException("Only a Branch Manager can void a transaction.");
        }

        var voided = transaction with { Status = TransactionStatus.Voided };
        await transactionRepository.UpdateAsync(voided, cancellationToken);
        TransactionVoided?.Invoke(this, new TransactionVoidedEvent(transactionId, actorStaffId, reason));
        return voided;
    }

    public async Task<CounterTransaction> RefundAsync(string transactionId, string actorStaffId, string reason, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.GetByIdAsync(transactionId, cancellationToken);
        if (transaction is null)
        {
            throw new InvalidOperationException("Transaction was not found.");
        }

        var staff = await staffRepository.GetByIdAsync(actorStaffId, cancellationToken);
        if (staff is null || staff.Role != StaffRole.BranchManager)
        {
            throw new InvalidOperationException("Only a Branch Manager can refund a transaction.");
        }

        var refunded = transaction with { Status = TransactionStatus.Refunded };
        await transactionRepository.UpdateAsync(refunded, cancellationToken);
        TransactionVoided?.Invoke(this, new TransactionVoidedEvent(transactionId, actorStaffId, reason));
        return refunded;
    }

    public async Task<MoneyOrder> CreateMoneyOrderAsync(string sender, string recipient, decimal amount, bool verified, CancellationToken cancellationToken = default)
    {
        var moneyOrder = new MoneyOrder(
            Guid.NewGuid().ToString("N"),
            amount,
            sender,
            recipient,
            5m,
            verified,
            verified ? Guid.NewGuid().ToString("N") : null);

        MoneyOrderIssued?.Invoke(this, new MoneyOrderIssuedEvent(moneyOrder.MoneyOrderId, amount, verified));
        return moneyOrder;
    }
}

public sealed class ReportService
{
    private readonly ITransactionRepository transactionRepository;
    private readonly IBranchRepository branchRepository;

    public ReportService(ITransactionRepository transactionRepository, IBranchRepository branchRepository)
    {
        this.transactionRepository = transactionRepository;
        this.branchRepository = branchRepository;
    }

    public async Task<IReadOnlyCollection<ReportSummary>> GetDailyBranchSalesAsync(BranchCode branchCode, CancellationToken cancellationToken = default)
    {
        var branch = await branchRepository.GetByCodeAsync(branchCode, cancellationToken);
        if (branch is null)
        {
            return Array.Empty<ReportSummary>();
        }

        var transactions = await transactionRepository.GetByBranchAsync(branchCode, cancellationToken);
        var retailTotal = transactions.Sum(t => t.Lines.Where(l => l.LineType == "Retail").Sum(l => l.ExtendedTotal.Amount));
        var serviceTotal = transactions.Sum(t => t.Lines.Where(l => l.LineType != "Retail").Sum(l => l.ExtendedTotal.Amount));

        return new[]
        {
            new ReportSummary("Retail sales", retailTotal, branchCode.Value),
            new ReportSummary("Service sales", serviceTotal, branchCode.Value)
        };
    }

    public async Task<IReadOnlyCollection<ReportSummary>> GetLowStockReportAsync(BranchCode branchCode, int threshold, CancellationToken cancellationToken = default)
    {
        var branch = await branchRepository.GetByCodeAsync(branchCode, cancellationToken);
        if (branch is null)
        {
            return Array.Empty<ReportSummary>();
        }

        var summaries = branch.Inventory
            .Where(entry => entry.Value <= threshold)
            .Select(entry => new ReportSummary(entry.Key.Value, entry.Value, branchCode.Value))
            .ToList();

        return summaries;
    }

    public async Task<IReadOnlyCollection<ReportSummary>> GetShippingVolumeReportAsync(CancellationToken cancellationToken = default)
    {
        var transactions = await transactionRepository.GetAllAsync(cancellationToken);
        return transactions
            .SelectMany(t => t.Lines)
            .Where(l => l.LineType == "Shipping")
            .GroupBy(l => l.Description)
            .Select(g => new ReportSummary(g.Key, g.Sum(line => line.UnitPrice.Amount), "zone-service"))
            .ToList();
    }
}

public sealed class DashboardService
{
    private readonly IBranchRepository branchRepository;
    private readonly IProductRepository productRepository;
    private readonly ITransactionRepository transactionRepository;

    public DashboardService(
        IBranchRepository branchRepository,
        IProductRepository productRepository,
        ITransactionRepository transactionRepository)
    {
        this.branchRepository = branchRepository;
        this.productRepository = productRepository;
        this.transactionRepository = transactionRepository;
    }

    public async Task<BranchDashboard> GetBranchDashboardAsync(BranchCode branchCode, CancellationToken cancellationToken = default)
    {
        var branchTask = branchRepository.GetByCodeAsync(branchCode, cancellationToken);
        var recentTransactionsTask = transactionRepository.GetByBranchAsync(branchCode, cancellationToken);
        var catalogTask = productRepository.GetAllAsync(cancellationToken);

        await Task.WhenAll(branchTask, recentTransactionsTask, catalogTask);

        var branch = branchTask.Result;
        var recentTransactions = recentTransactionsTask.Result;
        var products = catalogTask.Result;

        if (branch is null)
        {
            throw new InvalidOperationException("Branch not found.");
        }

        var lowStock = products
            .Where(product => branch.GetStock(product.Sku) <= 5)
            .ToList();

        var alerts = new List<AlertMessage>
        {
            new("Branch is open and ready for service.", "info")
        };

        if (lowStock.Count > 0)
        {
            alerts.Add(new AlertMessage($"{lowStock.Count} product(s) are below stock threshold.", "warning"));
        }

        return new BranchDashboard(branch, lowStock, recentTransactions, alerts);
    }
}

public sealed class StockLowEvent : EventArgs
{
    public StockLowEvent(BranchCode branchCode, Sku sku, int quantity)
    {
        BranchCode = branchCode;
        Sku = sku;
        Quantity = quantity;
    }

    public BranchCode BranchCode { get; }
    public Sku Sku { get; }
    public int Quantity { get; }
}

public sealed class TransactionCompletedEvent : EventArgs
{
    public TransactionCompletedEvent(string transactionId, decimal totalAmount)
    {
        TransactionId = transactionId;
        TotalAmount = totalAmount;
    }

    public string TransactionId { get; }
    public decimal TotalAmount { get; }
}

public sealed class TransactionVoidedEvent : EventArgs
{
    public TransactionVoidedEvent(string transactionId, string staffId, string reason)
    {
        TransactionId = transactionId;
        StaffId = staffId;
        Reason = reason;
    }

    public string TransactionId { get; }
    public string StaffId { get; }
    public string Reason { get; }
}

public sealed class MoneyOrderIssuedEvent : EventArgs
{
    public MoneyOrderIssuedEvent(string moneyOrderId, decimal amount, bool identityVerified)
    {
        MoneyOrderId = moneyOrderId;
        Amount = amount;
        IdentityVerified = identityVerified;
    }

    public string MoneyOrderId { get; }
    public decimal Amount { get; }
    public bool IdentityVerified { get; }
}
