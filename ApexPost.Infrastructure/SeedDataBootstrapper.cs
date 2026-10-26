using ApexPost.Application;
using ApexPost.Domain;

namespace ApexPost.Infrastructure;

public static class SeedDataBootstrapper
{
    public static async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var dataDir = SeedDataProvider.DataDirectory;
        Directory.CreateDirectory(dataDir);

        await EnsureFileAsync(Path.Combine(dataDir, "branches.json"), SeedDataProvider.GetBranches(), cancellationToken);
        await EnsureFileAsync(Path.Combine(dataDir, "staff.json"), SeedDataProvider.GetStaff(), cancellationToken);
        await EnsureFileAsync(Path.Combine(dataDir, "products.json"), SeedDataProvider.GetProducts(), cancellationToken);
        await EnsureFileAsync(Path.Combine(dataDir, "transactions.json"), SeedDataProvider.GetTransactions(), cancellationToken);
        await EnsureFileAsync(Path.Combine(dataDir, "zones.json"), SeedDataProvider.GetShippingZones(), cancellationToken);
        await EnsureFileAsync(Path.Combine(dataDir, "audit.json"), Array.Empty<AuditEntry>(), cancellationToken);
    }

    private static async Task EnsureFileAsync<T>(string filePath, IReadOnlyCollection<T> seeds, CancellationToken cancellationToken)
    {
        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 0)
            {
                if (typeof(T) != typeof(Staff))
                {
                    return;
                }

                var contents = await File.ReadAllTextAsync(filePath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(contents) && contents.Contains("PasswordHash", StringComparison.Ordinal) && contents.Contains("PasswordSalt", StringComparison.Ordinal))
                {
                    return;
                }
            }
        }

        await using var stream = File.Create(filePath);
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, seeds, ApexPostJsonOptions.Default, cancellationToken);
    }
}
