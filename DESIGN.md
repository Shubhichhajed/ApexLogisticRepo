# ApexPost Logistics Design

## 1. Architecture overview

ApexPost is implemented as a layered ASP.NET Core MVC solution with a domain-first design.

```text
+----------------------+
|   ApexPost.Web        |
|  MVC Controllers      |
|  Razor Views          |
|  Middleware           |
|  Filters              |
+----------+-----------+
		   |
		   v
+----------------------+
| ApexPost.Application  |
| Services / Orchestration |
| Domain Facades        |
| Pricing + Reports     |
+----------+-----------+
		   |
		   v
+----------------------+
| ApexPost.Domain       |
| Entities / Value     |
| Objects / Rules       |
| Repository Contracts  |
+----------+-----------+
		   |
		   v
+----------------------+
| ApexPost.Infrastructure|
| JSON Repositories     |
| Seed data             |
| Notifications         |
| Simulated services    |
+----------------------+
```

## 2. Domain model

### Core entities

- `Branch`
- `Staff`
- `Product`
- `CounterTransaction`
- `MoneyOrder`
- `IdentityVerification`
- `ShippingZone`

### Value objects

- `BranchCode`
- `Sku`
- `Money`
- `Weight`
- `Address`

### Design reasoning

The domain uses value objects for concepts that should be immutable and safe to compare by value instead of identity. This prevents invalid states such as `Money` with inconsistent rounding or `Sku` values being treated as free-form strings.

The transaction basket is modeled as a collection of `TransactionLine` entries with a `LineType` plus optional shipping metadata. This gives a single transaction the ability to mix:

- sold retail items
- shipping services
- money orders
- identity verification events

This is composition-based rather than inheritance-heavy, which keeps the model simpler and easier to extend.

## 3. Pricing and tax design

Pricing is expressed through a strategy-oriented pattern built on delegates and functional rules.

Examples from the project:

- `PricingStrategies.RetailPrice`
- `PricingStrategies.TaxRate`
- `ApexPostRules.CalculateShippingFee`

The benefits are:

- business rules are centralized
- configuration can be expressed as delegates
- the domain stays testable without ASP.NET MVC concerns

Shipping fees and tax calculations are based on branch rules and product metadata rather than hard-coded logic in controllers.

## 4. Middleware pipeline order

The app pipeline is intentionally ordered for security, observability, and routing correctness.

Order:

1. `UseForwardedHeaders`  
   Handles reverse-proxy header trust and TLS forwarding.

2. `UseRouting`  
   Establishes route matching.

3. `UseCors`  
   Allows only approved origin policies for partner scenarios.

4. `CorrelationIdMiddleware`  
   Ensures requests have a correlation ID for auditing and log correlation.

5. `RequestLoggingMiddleware`  
   Logs request metadata and timing.

6. `SecurityHeadersMiddleware`  
   Adds CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy.

7. `MaintenanceModeMiddleware`  
   Returns 503 with Retry-After when maintenance is enabled while allowing `/health`.

8. `IpRestrictionMiddleware` for admin/integration paths  
   Blocks unauthorized IPs before business endpoints process requests.

9. `ApiKeyValidationMiddleware`  
   Validates partner integration requests by API key.

10. `UseAuthentication`  
   Reads cookie identity and principal information.

11. `UseAuthorization`  
   Uses policies such as `BranchManagerOnly` and `SameBranchOnly`.

12. `UseRateLimiter`  
   Enforces login and partner rate limits.

13. `MapHealthChecks` and route endpoints  
   Exposes health endpoint and controller routes.

### Why this order matters

- Security and observability early in the pipeline prevent unsafe requests from reaching business logic.
- Maintenance mode is checked before controller work.
- Authorization happens after auth and before endpoint processing.
- Rate limiting is placed near the edge to reduce abuse.

## 5. Middleware vs action filters

| Concern | Best placement | Why |
|---|---|---|
| Correlation ID | Middleware | Applies to every request before controller execution |
| Security headers | Middleware | Process runs globally and early |
| Maintenance mode | Middleware | Blocks all requests before endpoint logic |
| API key validation | Middleware | Applies to integration routes before controller execution |
| IP restriction | Middleware | Works at the network boundary and is global for selected paths |
| Audit trail | Action Filter | Context-specific: controller, action, user, result, route values |
| Model validation | Action Filter | Validates action input and returns consistent model-state handling |
| Slow action logging | Action Filter | Action-specific performance measurement |

## 6. Action filters used

The following action filters are registered in MVC:

- `AuditActionFilter`
- `ValidationFilter`
- `PerformanceFilter`

### AuditActionFilter

This records:

- staff ID
- role
- branch
- controller/action
- route values
- result status
- duration
- correlation ID

It writes audit entries to an append-only JSON store behind `IAuditStore`.

### ValidationFilter

Returns consistent model-state validation messages and avoids duplicating validation logic in controllers.

### PerformanceFilter

Logs warnings for actions slower than a threshold and records status codes.

## 7. Security and audit

### Threats and mitigations

| Threat | Mitigation |
|---|---|
| Brute force login attempts | Fixed-window rate limiting on login POST |
| API key leakage | Stored in user-secrets, never in source control |
| Invalid partner requests | API key validation and partner-specific rate limit |
| Abuse from blocked IPs | IP restriction middleware on admin/integration routes |
| Cross-site scripting / clickjacking | Security headers middleware |
| Missing request traceability | Correlation IDs added to logs and response headers |
| Weak authorization | Cookie claims with role and branch checks |
| Audit data leakage | No passwords, IDs, API keys, or card data in audit payloads |

## 8. Advanced C# usage

The solution intentionally uses modern C# features to demonstrate real domain value and design patterns, including:

- records for immutable value objects
- pattern matching for business rule checks
- generic repository abstractions
- delegates for pricing and rules
- events for notification scenarios
- reflection-based validation metadata
- async/await with cancellation tokens
- `Task.WhenAll`-style orchestration patterns in service logic
- `SemaphoreSlim` in file-write access paths
- `ValueTask` and caching concepts for frequently-read metadata

## 9. Persistence model

No database or ORM is used this week. The persistence layer uses JSON files behind interfaces:

- `IBranchRepository`
- `IStaffRepository`
- `IProductRepository`
- `ITransactionRepository`
- `IAuditStore`

This allows the storage implementation to change without altering business logic.

## 10. SOLID mapping

### Single Responsibility

- Each service has a focused responsibility such as inventory, authentication, transactions, or reports.

### Open/Closed

- New shipping rules or tax rules can be added through delegates or service extensions without modifying the entire domain model.

### Liskov Substitution

- Repository implementations conform to the same contract and can be consumed interchangeably by the application layer.

### Interface Segregation

- Interfaces are small and focused around repository capabilities and audit behavior.

### Dependency Inversion

- Controllers and services depend on interfaces, not concrete JSON implementations.

## 11. ADRs (Architecture Decision Records)

### ADR 1: Use layered architecture for separation of concerns
We split the solution into Domain, Application, Infrastructure, and Web layers to keep business logic independent from HTTP and persistence.

### ADR 2: Use value objects for domain identity
We modeled `Sku`, `BranchCode`, `Money`, and `Weight` as value objects to reduce mutation risk and centralize validation.

### ADR 3: Use JSON files instead of a database for this iteration
This satisfies the no-database requirement while preserving a repository abstraction that can later be replaced with SQL or EF Core.

### ADR 4: Use cookie authentication with custom claims
The app uses simple cookie auth and claims for `StaffId`, `Role`, and `BranchCode` instead of ASP.NET Core Identity, as required by the lab.

### ADR 5: Put security decisions in middleware rather than controllers
Headers, rate limiting, IP restrictions, and API key checks are at the edge so they are enforced before business logic executes.

### ADR 6: Use action filters for auditing and validation
Action filters provide access to routing data, controller context, and user context in a way that is appropriate for cross-cutting concerns.

### ADR 7: Centralize pricing and tax logic in delegates and domain rules
This makes pricing behavior easier to test and easier to evolve without touching controllers.

### ADR 8: Avoid direct file writes from controllers
All repository persistence goes through the infrastructure layer so file I/O and locking remain centralized and safe.

## 12. Seed data

The solution seeds:

- 5 branches
- 20 staff members across roles
- 30+ products
- shipping zones and rates
- sample transaction history

The generated JSON files live under the app’s data directory and are created automatically on startup if missing.

## 13. Operational notes

- Health endpoint: `/health`
- Login page: `/login`
- Dashboard: `/home/dashboard`
- Products: `/products`
- Reports: `/reports`
- Integration endpoints: `/integration/...`

## 14. Future improvements

- Add first-class branch manager and auditor workflows
- Split the current transaction flow into add-item, review, and finalise steps
- Replace JSON persistence with SQL Server or PostgreSQL
- Add stronger authorization policy enforcement per branch and role
- Expand report generation and export capabilities
