using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Domain;

public enum StaffRole
{
    CounterClerk,
    BranchManager,
    Auditor,
    Administrator
}

public enum ProductCategory
{
    Packaging,
    Stationery,
    OfficeSupplies
}

public enum TaxCategory
{
    Standard,
    Reduced,
    Exempt
}

public enum ServiceLevel
{
    Standard,
    Express,
    Registered
}

public enum PaymentMethod
{
    Cash,
    Card,
    DigitalWallet,
    MoneyOrder
}

public enum TransactionStatus
{
    Open,
    Paid,
    Voided,
    Refunded
}

public sealed record BranchCode(string Value)
{
    public string Value { get; init; } = Value.Trim();

    public override string ToString() => Value;

    public static BranchCode Parse(string raw) => new(raw.Trim());
}

public sealed record Sku(string Value)
{
    public string Value { get; init; } = Value.Trim();

    public override string ToString() => Value;

    public static Sku Parse(string raw) => new(raw.Trim());
}

public sealed class SkuJsonConverter : JsonConverter<Sku>
{
    public override Sku? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : Sku.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, Sku value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }

    public override Sku ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Read(ref reader, typeToConvert, options) ?? throw new JsonException("Sku value cannot be null.");

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Sku value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value);
}

public sealed class BranchCodeJsonConverter : JsonConverter<BranchCode>
{
    public override BranchCode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : BranchCode.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, BranchCode value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }

    public override BranchCode ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Read(ref reader, typeToConvert, options) ?? throw new JsonException("BranchCode value cannot be null.");

    public override void WriteAsPropertyName(Utf8JsonWriter writer, BranchCode value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Value);
}

public static class ApexPostJsonOptions
{
    public static readonly JsonSerializerOptions Default = Build();

    private static JsonSerializerOptions Build()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        options.Converters.Add(new SkuJsonConverter());
        options.Converters.Add(new BranchCodeJsonConverter());
        return options;
    }
}

public sealed record Weight(decimal Kilograms)
{
    public decimal Kilograms { get; init; } = Math.Round(Kilograms, 3, MidpointRounding.AwayFromZero);

    public override string ToString() => $"{Kilograms:0.###} kg";
}

public sealed record Money(decimal Amount)
{
    public decimal Amount { get; init; } = Math.Round(Amount, 2, MidpointRounding.AwayFromZero);

    public static Money Zero => new(0m);

    public Money Add(Money other) => new(Amount + other.Amount);

    public Money Subtract(Money other) => new(Amount - other.Amount);

    public Money Multiply(decimal factor) => new(Math.Round(Amount * factor, 2, MidpointRounding.AwayFromZero));

    public decimal TaxedValue(decimal rate) => Math.Round(Amount * rate, 2, MidpointRounding.AwayFromZero);

    public override string ToString() => $"{Amount:C2}";
}

public sealed record Address(string Line1, string City, string Region, string PostalCode)
{
    public string Line1 { get; init; } = Line1;
    public string City { get; init; } = City;
    public string Region { get; init; } = Region;
    public string PostalCode { get; init; } = PostalCode;
}

public sealed record CounterTerminal(string TerminalId, bool IsOpen = true);

public sealed record Branch(
    BranchCode Code,
    string Name,
    string Region,
    string OpeningHours,
    bool IsActive,
    IReadOnlyCollection<CounterTerminal> Counters,
    Dictionary<Sku, int> Inventory)
{
    public BranchCode Code { get; init; } = Code;
    public string Name { get; init; } = Name;
    public string Region { get; init; } = Region;
    public string OpeningHours { get; init; } = OpeningHours;
    public bool IsActive { get; init; } = IsActive;
    public IReadOnlyCollection<CounterTerminal> Counters { get; init; } = Counters;
    public Dictionary<Sku, int> Inventory { get; init; } = Inventory;

    public int GetStock(Sku productSku)
    {
        return Inventory.TryGetValue(productSku, out var quantity) ? quantity : 0;
    }

    public void AdjustStock(Sku productSku, int delta)
    {
        var existing = GetStock(productSku);
        var newQuantity = existing + delta;
        if (newQuantity < 0)
        {
            throw new InvalidOperationException($"Stock cannot go negative for {productSku}.");
        }

        Inventory[productSku] = newQuantity;
    }
}

public sealed record Staff(
    string StaffId,
    string Name,
    string Email,
    StaffRole Role,
    BranchCode HomeBranch,
    bool IsActive,
    string PasswordHash = "",
    string PasswordSalt = "")
{
    public string StaffId { get; init; } = StaffId;
    public string Name { get; init; } = Name;
    public string Email { get; init; } = Email;
    public StaffRole Role { get; init; } = Role;
    public BranchCode HomeBranch { get; init; } = HomeBranch;
    public bool IsActive { get; init; } = IsActive;
    public string PasswordHash { get; init; } = PasswordHash;
    public string PasswordSalt { get; init; } = PasswordSalt;
}

public sealed record Product(
    Sku Sku,
    string Name,
    ProductCategory Category,
    Money UnitPrice,
    TaxCategory TaxCategory,
    bool IsActive = true)
{
    public Sku Sku { get; init; } = Sku;
    public string Name { get; init; } = Name;
    public ProductCategory Category { get; init; } = Category;
    public Money UnitPrice { get; init; } = UnitPrice;
    public TaxCategory TaxCategory { get; init; } = TaxCategory;
    public bool IsActive { get; init; } = IsActive;
}

public sealed record TransactionLine(
    string Id,
    string Description,
    string LineType,
    int Quantity,
    Money UnitPrice,
    decimal TaxAmount,
    string? ServiceLevel = null,
    decimal? WeightKg = null)
{
    public string Id { get; init; } = Id;
    public string Description { get; init; } = Description;
    public string LineType { get; init; } = LineType;
    public int Quantity { get; init; } = Quantity;
    public Money UnitPrice { get; init; } = UnitPrice;
    public decimal TaxAmount { get; init; } = TaxAmount;
    public string? ServiceLevel { get; init; } = ServiceLevel;
    public decimal? WeightKg { get; init; } = WeightKg;

    public Money ExtendedTotal => UnitPrice.Multiply(Quantity).Add(new Money(TaxAmount));
}

public sealed record IdentityVerification(
    string VerificationId,
    string DocumentType,
    bool IsSuccessful,
    string MaskedDocumentNumber,
    DateTime VerifiedAtUtc)
{
    public string VerificationId { get; init; } = VerificationId;
    public string DocumentType { get; init; } = DocumentType;
    public bool IsSuccessful { get; init; } = IsSuccessful;
    public string MaskedDocumentNumber { get; init; } = MaskedDocumentNumber;
    public DateTime VerifiedAtUtc { get; init; } = VerifiedAtUtc;
}

public sealed record MoneyOrder(
    string MoneyOrderId,
    decimal Amount,
    string SenderName,
    string RecipientName,
    decimal Fee,
    bool IdentityVerified,
    string? VerificationId = null)
{
    public string MoneyOrderId { get; init; } = MoneyOrderId;
    public decimal Amount { get; init; } = Amount;
    public string SenderName { get; init; } = SenderName;
    public string RecipientName { get; init; } = RecipientName;
    public decimal Fee { get; init; } = Fee;
    public bool IdentityVerified { get; init; } = IdentityVerified;
    public string? VerificationId { get; init; } = VerificationId;
}

public sealed record CounterTransaction(
    string TransactionId,
    BranchCode BranchCode,
    string CounterId,
    string ClerkStaffId,
    List<TransactionLine> Lines,
    PaymentMethod PaymentMethod,
    TransactionStatus Status,
    DateTime CreatedAtUtc,
    string? CorrelationId = null)
{
    public string TransactionId { get; init; } = TransactionId;
    public BranchCode BranchCode { get; init; } = BranchCode;
    public string CounterId { get; init; } = CounterId;
    public string ClerkStaffId { get; init; } = ClerkStaffId;
    public List<TransactionLine> Lines { get; init; } = Lines;
    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod;
    public TransactionStatus Status { get; init; } = Status;
    public DateTime CreatedAtUtc { get; init; } = CreatedAtUtc;
    public string? CorrelationId { get; init; } = CorrelationId;

    public Money Subtotal => new(Lines.Sum(line => line.UnitPrice.Amount * line.Quantity));

    public Money TaxTotal => new(Lines.Sum(line => line.TaxAmount));

    public Money GrandTotal => Subtotal.Add(new Money(TaxTotal.Amount));

    public bool RequiresIdentityVerification(decimal threshold) =>
        Lines.Any(line => line.LineType == "MoneyOrder") && GrandTotal.Amount > threshold;
}

public sealed record ShippingZone(
    string ZoneCode,
    string Name,
    decimal RatePerKg,
    decimal FixedRate)
{
    public string ZoneCode { get; init; } = ZoneCode;
    public string Name { get; init; } = Name;
    public decimal RatePerKg { get; init; } = RatePerKg;
    public decimal FixedRate { get; init; } = FixedRate;
}

public sealed record AuditEntry(
    string AuditId,
    string StaffId,
    string Role,
    string BranchCode,
    string Controller,
    string Action,
    string? RouteValues,
    int StatusCode,
    double DurationMs,
    string CorrelationId,
    string EventName,
    string Payload,
    DateTime TimestampUtc)
{
    public string AuditId { get; init; } = AuditId;
    public string StaffId { get; init; } = StaffId;
    public string Role { get; init; } = Role;
    public string BranchCode { get; init; } = BranchCode;
    public string Controller { get; init; } = Controller;
    public string Action { get; init; } = Action;
    public string? RouteValues { get; init; } = RouteValues;
    public int StatusCode { get; init; } = StatusCode;
    public double DurationMs { get; init; } = DurationMs;
    public string CorrelationId { get; init; } = CorrelationId;
    public string EventName { get; init; } = EventName;
    public string Payload { get; init; } = Payload;
    public DateTime TimestampUtc { get; init; } = TimestampUtc;
}

public static class PricingStrategies
{
    public static readonly Func<Product, decimal, Money> RetailPrice = (product, quantity) =>
        product.UnitPrice.Multiply(quantity);

    public static readonly Func<Product, decimal, decimal> TaxRate = (product, quantity) => product.TaxCategory switch
    {
        TaxCategory.Standard => 0.2m,
        TaxCategory.Reduced => 0.1m,
        TaxCategory.Exempt => 0m,
        _ => 0m
    };

    public static readonly Predicate<int> LowStockThreshold = stock => stock <= 5;
}

public static class ApexPostRules
{
    public const decimal MoneyOrderThreshold = 500m;
    public const decimal ParcelMaxWeightForStandard = 2.5m;
    public const decimal ParcelMaxWeightForExpress = 10m;
    public const decimal ParcelMaxWeightForRegistered = 25m;

    public static bool HasSufficientStock(Branch branch, Sku sku, int requestedQuantity)
    {
        return branch.GetStock(sku) >= requestedQuantity;
    }

    public static bool IsStaffAllowedToProcess(Staff staff, Branch branch)
    {
        return staff.IsActive && branch.IsActive && staff.HomeBranch == branch.Code;
    }

    public static bool CanCompleteMoneyOrder(decimal amount, bool hasSuccessfulVerification)
    {
        if (amount <= MoneyOrderThreshold)
        {
            return true;
        }

        return hasSuccessfulVerification;
    }

    public static bool CanShipParcel(Weight weight, ServiceLevel level) => level switch
    {
        ServiceLevel.Standard => weight.Kilograms <= ParcelMaxWeightForStandard,
        ServiceLevel.Express => weight.Kilograms <= ParcelMaxWeightForExpress,
        ServiceLevel.Registered => weight.Kilograms <= ParcelMaxWeightForRegistered,
        _ => false
    };

    public static decimal CalculateShippingFee(ShippingZone zone, Weight weight, ServiceLevel level)
    {
        var multiplier = level switch
        {
            ServiceLevel.Standard => 1m,
            ServiceLevel.Express => 1.8m,
            ServiceLevel.Registered => 2.4m,
            _ => 1m
        };

        var fee = zone.FixedRate + (zone.RatePerKg * weight.Kilograms * multiplier);
        return Math.Round(fee, 2, MidpointRounding.AwayFromZero);
    }

    public static bool IsBranchOpen(Branch branch) => branch.IsActive;

    public static bool RequiresManagerForVoidOrRefund(Staff staff) => staff.Role == StaffRole.BranchManager;
}

public interface IRepository<T> where T : class
{
    Task<IReadOnlyCollection<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(T item, CancellationToken cancellationToken = default);
    Task UpdateAsync(T item, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public interface ISpecification<T> where T : class
{
    Expression<Func<T, bool>>? Predicate { get; }
}

public sealed class Specification<T> : ISpecification<T> where T : class
{
    public Specification(Expression<Func<T, bool>>? predicate)
    {
        Predicate = predicate;
    }

    public Expression<Func<T, bool>>? Predicate { get; }
}

public static class ReflectionValidator
{
    public static IReadOnlyCollection<string> Validate<T>(T instance)
    {
        var properties = typeof(T).GetProperties();
        var validationResults = new List<string>();

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttributes(typeof(RequiredForAuditAttribute), inherit: true).FirstOrDefault();
            if (attribute is null)
            {
                continue;
            }

            var value = property.GetValue(instance);
            if (value is null || value is string s && string.IsNullOrWhiteSpace(s))
            {
                validationResults.Add($"{property.Name} is required.");
            }
        }

        return validationResults;
    }
}

[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class RequiredForAuditAttribute : Attribute;

public static class DecimalExtensions
{
    public static decimal RoundCurrency(this decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public static class MoneyExtensions
{
    public static Money ToMoney(this decimal value) => new(value.RoundCurrency());
}

public static class WeightExtensions
{
    public static bool IsOverLimit(this Weight weight, ServiceLevel serviceLevel) => !ApexPostRules.CanShipParcel(weight, serviceLevel);
}
