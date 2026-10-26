using ApexPost.Application;
using ApexPost.Domain;
using ApexPost.Infrastructure;
using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<AuditActionFilter>();
    options.Filters.AddService<ValidationFilter>();
    options.Filters.AddService<PerformanceFilter>();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BranchManagerOnly", policy => policy.RequireClaim("Role", StaffRole.BranchManager.ToString()));
    options.AddPolicy("SameBranchOnly", policy => policy.RequireClaim("BranchCode"));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("PartnerWidget", policy =>
    {
        policy.WithOrigins("https://tracking-widget.apexpost.test", "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    options.AddPolicy("partner", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Request.Headers["X-Partner-Id"].ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 25,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

builder.Services.AddHealthChecks();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IBranchRepository>(sp => new BranchJsonRepository(Path.Combine(AppContext.BaseDirectory, "Data", "branches.json")));
builder.Services.AddSingleton<IStaffRepository>(sp => new StaffJsonRepository(Path.Combine(AppContext.BaseDirectory, "Data", "staff.json")));
builder.Services.AddSingleton<IProductRepository>(sp => new ProductJsonRepository(Path.Combine(AppContext.BaseDirectory, "Data", "products.json")));
builder.Services.AddSingleton<ITransactionRepository>(sp => new TransactionJsonRepository(Path.Combine(AppContext.BaseDirectory, "Data", "transactions.json")));
builder.Services.AddSingleton<IAuditStore>(sp => new JsonAuditStore(Path.Combine(AppContext.BaseDirectory, "Data", "audit.json")));
builder.Services.AddSingleton<CourierRateCache>();
builder.Services.AddSingleton<NotificationService>();

builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PricingService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<AuditActionFilter>();
builder.Services.AddScoped<ValidationFilter>();
builder.Services.AddScoped<PerformanceFilter>();

var app = builder.Build();

await SeedDataBootstrapper.InitializeAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    RequireHeaderSymmetry = false,
    ForwardLimit = 2
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("PartnerWidget");
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<MaintenanceModeMiddleware>();
app.UseWhen(context => context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase) || context.Request.Path.StartsWithSegments("/integration", StringComparison.OrdinalIgnoreCase), branch =>
{
    branch.UseMiddleware<IpRestrictionMiddleware>();
});
app.UseMiddleware<ApiKeyValidationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
