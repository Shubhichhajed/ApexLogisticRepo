using System.Text.Json;
using ApexPost.Application;
using ApexPost.Domain;

namespace ApexPost.Infrastructure;

public class JsonRepository<T> : IRepository<T> where T : class
{
    private readonly string filePath;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public JsonRepository(string filePath)
    {
        this.filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? AppContext.BaseDirectory);
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, "[]");
        }
    }

    public async Task<IReadOnlyCollection<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return Array.Empty<T>();
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            return Array.Empty<T>();
        }

        await using var stream = File.OpenRead(filePath);
        var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, ApexPostJsonOptions.Default, cancellationToken);
        return items ?? new List<T>();
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var items = await GetAllAsync(cancellationToken);
        return items.FirstOrDefault(item => GetKey(item) == id);
    }

    public async Task AddAsync(T item, CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var items = (await GetAllAsync(cancellationToken)).ToList();
            if (!items.Any(existing => GetKey(existing) == GetKey(item)))
            {
                items.Add(item);
                await SaveAllAsync(items, cancellationToken);
            }
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task UpdateAsync(T item, CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var items = (await GetAllAsync(cancellationToken)).ToList();
            var index = items.FindIndex(existing => GetKey(existing) == GetKey(item));
            if (index >= 0)
            {
                items[index] = item;
            }
            else
            {
                items.Add(item);
            }

            await SaveAllAsync(items, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var items = (await GetAllAsync(cancellationToken)).ToList();
            var filtered = items.Where(item => GetKey(item) != id).ToList();
            await SaveAllAsync(filtered, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    protected virtual string GetKey(T item) => item switch
    {
        Branch branch => branch.Code.Value,
        Staff staff => staff.StaffId,
        Product product => product.Sku.Value,
        CounterTransaction transaction => transaction.TransactionId,
        _ => item.GetType().Name
    };

    protected async Task SaveAllAsync(IEnumerable<T> items, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, items, ApexPostJsonOptions.Default, cancellationToken);
    }
}

public sealed class BranchJsonRepository : JsonRepository<Branch>, IBranchRepository
{
    public BranchJsonRepository(string filePath) : base(filePath) { }

    public async Task<Branch?> GetByCodeAsync(BranchCode code, CancellationToken cancellationToken = default)
    {
        var branches = await GetAllAsync(cancellationToken);
        return branches.FirstOrDefault(branch => branch.Code == code);
    }
}

public sealed class StaffJsonRepository : JsonRepository<Staff>, IStaffRepository
{
    public StaffJsonRepository(string filePath) : base(filePath) { }

    public async Task<Staff?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var staff = await GetAllAsync(cancellationToken);
        return staff.FirstOrDefault(item => string.Equals(item.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyCollection<Staff>> GetByBranchAsync(BranchCode branchCode, CancellationToken cancellationToken = default)
    {
        var staff = await GetAllAsync(cancellationToken);
        return staff.Where(item => item.HomeBranch == branchCode).ToList();
    }
}

public sealed class ProductJsonRepository : JsonRepository<Product>, IProductRepository
{
    public ProductJsonRepository(string filePath) : base(filePath) { }

    public async Task<Product?> GetBySkuAsync(Sku sku, CancellationToken cancellationToken = default)
    {
        var products = await GetAllAsync(cancellationToken);
        return products.FirstOrDefault(product => product.Sku == sku);
    }
}

public sealed class TransactionJsonRepository : JsonRepository<CounterTransaction>, ITransactionRepository
{
    public TransactionJsonRepository(string filePath) : base(filePath) { }

    public async Task<IReadOnlyCollection<CounterTransaction>> GetByBranchAsync(BranchCode branchCode, CancellationToken cancellationToken = default)
    {
        var transactions = await GetAllAsync(cancellationToken);
        return transactions.Where(transaction => transaction.BranchCode == branchCode).ToList();
    }
}

public sealed class JsonAuditStore : IAuditStore
{
    private readonly string path;
    private readonly SemaphoreSlim writeLock = new(1, 1);

    public JsonAuditStore(string path)
    {
        this.path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppContext.BaseDirectory);
        if (!File.Exists(path))
        {
            File.WriteAllText(path, "[]");
        }
    }

    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await writeLock.WaitAsync(cancellationToken);
        try
        {
            var entries = await GetAllAsync(cancellationToken);
            var list = entries.ToList();
            list.Add(entry);
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, list, ApexPostJsonOptions.Default, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public async Task<IReadOnlyCollection<AuditEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<AuditEntry>();
        }

        await using var stream = File.OpenRead(path);
        var entries = await JsonSerializer.DeserializeAsync<List<AuditEntry>>(stream, ApexPostJsonOptions.Default, cancellationToken);
        return entries ?? new List<AuditEntry>();
    }
}

public sealed class SeedDataProvider
{
    public static readonly string DataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

    public static IReadOnlyCollection<Branch> GetBranches()
    {
        return new[]
        {
            new Branch(BranchCode.Parse("BR-001"), "Downtown", "North", "08:00-18:00", true, new[] { new CounterTerminal("C-01"), new CounterTerminal("C-02") }, new Dictionary<Sku, int> { [Sku.Parse("SKU-001")] = 18, [Sku.Parse("SKU-002")] = 25, [Sku.Parse("SKU-003")] = 10, [Sku.Parse("SKU-010")] = 9 }),
            new Branch(BranchCode.Parse("BR-002"), "Harbor", "Coastal", "09:00-17:00", true, new[] { new CounterTerminal("C-03") }, new Dictionary<Sku, int> { [Sku.Parse("SKU-004")] = 12, [Sku.Parse("SKU-005")] = 7, [Sku.Parse("SKU-006")] = 20, [Sku.Parse("SKU-011")] = 6 }),
            new Branch(BranchCode.Parse("BR-003"), "Hillview", "Central", "08:30-18:30", true, new[] { new CounterTerminal("C-04"), new CounterTerminal("C-05") }, new Dictionary<Sku, int> { [Sku.Parse("SKU-007")] = 15, [Sku.Parse("SKU-008")] = 11, [Sku.Parse("SKU-009")] = 17, [Sku.Parse("SKU-012")] = 8 }),
            new Branch(BranchCode.Parse("BR-004"), "Market East", "East", "07:30-16:30", true, new[] { new CounterTerminal("C-06") }, new Dictionary<Sku, int> { [Sku.Parse("SKU-013")] = 9, [Sku.Parse("SKU-014")] = 14, [Sku.Parse("SKU-015")] = 12, [Sku.Parse("SKU-016")] = 7 }),
            new Branch(BranchCode.Parse("BR-005"), "Ridgeway", "West", "08:00-17:00", false, new[] { new CounterTerminal("C-07") }, new Dictionary<Sku, int> { [Sku.Parse("SKU-017")] = 8, [Sku.Parse("SKU-018")] = 22, [Sku.Parse("SKU-019")] = 13, [Sku.Parse("SKU-020")] = 5 })
        };
    }

    public static IReadOnlyCollection<Staff> GetStaff()
    {
        return new[]
        {
            new Staff("ST-1001", "Alicia Moore", "alicia.moore@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-001"), true),
            new Staff("ST-1002", "Ben Patel", "ben.patel@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-001"), true),
            new Staff("ST-1003", "Carmen Lee", "carmen.lee@apexpost.test", StaffRole.BranchManager, BranchCode.Parse("BR-001"), true),
            new Staff("ST-1004", "Darren Ross", "darren.ross@apexpost.test", StaffRole.Auditor, BranchCode.Parse("BR-001"), true),
            new Staff("ST-1005", "Elena Park", "elena.park@apexpost.test", StaffRole.Administrator, BranchCode.Parse("BR-001"), true),
            new Staff("ST-2001", "Frank Hall", "frank.hall@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-002"), true),
            new Staff("ST-2002", "Grace Nguyen", "grace.nguyen@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-002"), true),
            new Staff("ST-2003", "Hugo Singh", "hugo.singh@apexpost.test", StaffRole.BranchManager, BranchCode.Parse("BR-002"), true),
            new Staff("ST-2004", "Ingrid Mills", "ingrid.mills@apexpost.test", StaffRole.Auditor, BranchCode.Parse("BR-002"), true),
            new Staff("ST-2005", "Jack Ortiz", "jack.ortiz@apexpost.test", StaffRole.Administrator, BranchCode.Parse("BR-002"), true),
            new Staff("ST-3001", "Kira Cole", "kira.cole@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-003"), true),
            new Staff("ST-3002", "Luca Green", "luca.green@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-003"), true),
            new Staff("ST-3003", "Maya Diaz", "maya.diaz@apexpost.test", StaffRole.BranchManager, BranchCode.Parse("BR-003"), true),
            new Staff("ST-3004", "Noah Reed", "noah.reed@apexpost.test", StaffRole.Auditor, BranchCode.Parse("BR-003"), true),
            new Staff("ST-3005", "Olivia Price", "olivia.price@apexpost.test", StaffRole.Administrator, BranchCode.Parse("BR-003"), true),
            new Staff("ST-4001", "Priya Shaw", "priya.shaw@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-004"), true),
            new Staff("ST-4002", "Quinn Frost", "quinn.frost@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-004"), true),
            new Staff("ST-4003", "Ravi Shah", "ravi.shah@apexpost.test", StaffRole.BranchManager, BranchCode.Parse("BR-004"), true),
            new Staff("ST-4004", "Sonia Cruz", "sonia.cruz@apexpost.test", StaffRole.Auditor, BranchCode.Parse("BR-004"), true),
            new Staff("ST-4005", "Tariq Ali", "tariq.ali@apexpost.test", StaffRole.Administrator, BranchCode.Parse("BR-004"), true),
            new Staff("ST-5001", "Uma Bell", "uma.bell@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-005"), false),
            new Staff("ST-5002", "Victor Moss", "victor.moss@apexpost.test", StaffRole.CounterClerk, BranchCode.Parse("BR-005"), false),
            new Staff("ST-5003", "Willa Stone", "willa.stone@apexpost.test", StaffRole.BranchManager, BranchCode.Parse("BR-005"), false),
            new Staff("ST-5004", "Xander King", "xander.king@apexpost.test", StaffRole.Auditor, BranchCode.Parse("BR-005"), false),
            new Staff("ST-5005", "Yara Finch", "yara.finch@apexpost.test", StaffRole.Administrator, BranchCode.Parse("BR-005"), false)
        };
    }

    public static IReadOnlyCollection<Product> GetProducts()
    {
        return new[]
        {
            new Product(Sku.Parse("SKU-001"), "Parcel Box Small", ProductCategory.Packaging, new Money(4.50m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-002"), "Bubble Wrap Roll", ProductCategory.Packaging, new Money(9.00m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-003"), "Priority Mailer", ProductCategory.Packaging, new Money(3.25m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-004"), "A4 Copy Paper", ProductCategory.Stationery, new Money(7.60m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-005"), "Ballpoint Pen Pack", ProductCategory.Stationery, new Money(5.10m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-006"), "Sticky Notes", ProductCategory.Stationery, new Money(2.80m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-007"), "Desk Organizer", ProductCategory.OfficeSupplies, new Money(14.00m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-008"), "Stapler", ProductCategory.OfficeSupplies, new Money(11.50m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-009"), "USB Cable", ProductCategory.OfficeSupplies, new Money(8.40m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-010"), "Envelope Pack", ProductCategory.Packaging, new Money(3.80m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-011"), "Document Folder", ProductCategory.Stationery, new Money(4.20m), TaxCategory.Reduced),
            new Product(Sku.Parse("SKU-012"), "Index Tabs", ProductCategory.Stationery, new Money(2.90m), TaxCategory.Reduced),
            new Product(Sku.Parse("SKU-013"), "Notebook A5", ProductCategory.Stationery, new Money(6.20m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-014"), "Packing Tape", ProductCategory.Packaging, new Money(6.45m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-015"), "Courier Tape", ProductCategory.Packaging, new Money(5.75m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-016"), "Post-it Notes", ProductCategory.Stationery, new Money(3.95m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-017"), "Office Scissors", ProductCategory.OfficeSupplies, new Money(9.90m), TaxCategory.Reduced),
            new Product(Sku.Parse("SKU-018"), "Calculator", ProductCategory.OfficeSupplies, new Money(18.25m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-019"), "Printer Paper", ProductCategory.OfficeSupplies, new Money(12.00m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-020"), "Shipping Labels", ProductCategory.Packaging, new Money(2.60m), TaxCategory.Exempt),
            new Product(Sku.Parse("SKU-021"), "Letter Envelopes", ProductCategory.Packaging, new Money(4.35m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-022"), "Receipt Paper", ProductCategory.Stationery, new Money(5.55m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-023"), "File Divider", ProductCategory.OfficeSupplies, new Money(7.10m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-024"), "Packing Peanuts", ProductCategory.Packaging, new Money(6.30m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-025"), "Plastic Mailer", ProductCategory.Packaging, new Money(3.10m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-026"), "Flexible Tubs", ProductCategory.Packaging, new Money(8.80m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-027"), "Ink Cartridge", ProductCategory.OfficeSupplies, new Money(22.50m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-028"), "Rubber Stamp", ProductCategory.OfficeSupplies, new Money(10.45m), TaxCategory.Standard),
            new Product(Sku.Parse("SKU-029"), "Pencil Box", ProductCategory.Stationery, new Money(4.50m), TaxCategory.Reduced),
            new Product(Sku.Parse("SKU-030"), "Business Card Holder", ProductCategory.OfficeSupplies, new Money(11.20m), TaxCategory.Standard)
        };
    }

    public static IReadOnlyCollection<ShippingZone> GetShippingZones()
    {
        return new[]
        {
            new ShippingZone("Z1", "Local", 1.20m, 4.50m),
            new ShippingZone("Z2", "Metro", 1.40m, 5.00m),
            new ShippingZone("Z3", "Regional", 2.10m, 7.25m),
            new ShippingZone("Z4", "National", 2.80m, 8.90m),
            new ShippingZone("Z5", "Cross Border", 4.20m, 13.50m),
            new ShippingZone("Z6", "Remote", 5.90m, 16.50m),
            new ShippingZone("Z7", "International", 8.20m, 21.60m),
            new ShippingZone("Z8", "Rural", 3.10m, 9.80m),
            new ShippingZone("Z9", "Express Hub", 4.80m, 11.20m),
            new ShippingZone("Z10", "Priority", 6.40m, 18.90m)
        };
    }

    public static IReadOnlyCollection<CounterTransaction> GetTransactions()
    {
        var now = DateTime.UtcNow;
        var transactions = new List<CounterTransaction>();

        for (var i = 1; i <= 40; i++)
        {
            var branchCode = BranchCode.Parse($"BR-{(i % 5) + 1:000}");
            var line1 = new TransactionLine($"L-{i}-1", "Retail", "Retail", 2, new Money(4.50m + i % 10), 1.80m + (i % 4));
            var line2 = new TransactionLine($"L-{i}-2", "Shipping", "Shipping", 1, new Money(9.00m + i % 5), 1.90m + (i % 3), ServiceLevel.Standard.ToString(), 1.2m + (i % 4));
            var transaction = new CounterTransaction(
                $"TX-{i:0000}",
                branchCode,
                $"C-{i % 7 + 1}",
                $"ST-{(i % 20) + 1001}",
                new List<TransactionLine> { line1, line2 },
                i % 3 == 0 ? PaymentMethod.Card : PaymentMethod.Cash,
                i % 4 == 0 ? TransactionStatus.Paid : TransactionStatus.Open,
                now.AddDays(-(i % 12)).AddHours(-(i % 6)));

            transactions.Add(transaction);
        }

        return transactions;
    }
}

public sealed class CourierRateCache
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly Dictionary<string, decimal> zoneRates = new();

    public async ValueTask<decimal> GetRateAsync(string zoneCode, CancellationToken cancellationToken = default)
    {
        if (zoneRates.TryGetValue(zoneCode, out var cachedRate))
        {
            return cachedRate;
        }

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (zoneRates.TryGetValue(zoneCode, out cachedRate))
            {
                return cachedRate;
            }

            var rate = SeedDataProvider.GetShippingZones().First(z => z.ZoneCode == zoneCode).RatePerKg;
            zoneRates[zoneCode] = rate;
            return rate;
        }
        finally
        {
            semaphore.Release();
        }
    }
}

public sealed class NotificationService
{
    public event EventHandler<StockLowEvent>? StockLow;
    public event EventHandler<MoneyOrderIssuedEvent>? HighValueMoneyOrder;
    public event EventHandler<TransactionVoidedEvent>? VoidOrRefund;

    public void PublishStockLow(StockLowEvent args) => StockLow?.Invoke(this, args);

    public void PublishMoneyOrder(MoneyOrderIssuedEvent args)
    {
        if (args.Amount >= 1000m)
        {
            HighValueMoneyOrder?.Invoke(this, args);
        }
    }

    public void PublishVoidOrRefund(TransactionVoidedEvent args) => VoidOrRefund?.Invoke(this, args);
}
