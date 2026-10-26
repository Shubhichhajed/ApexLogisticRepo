# ApexPost Logistics

ApexPost Logistics is a .NET 10 ASP.NET Core MVC sample solution for a national postal and retail network. The application models branch operations, staff, inventory, shipping, money orders, identity verification, reports, and transaction processing in a layered architecture using JSON persistence instead of a database.

## Solution structure

- `ApexPost.Domain` – core domain model, value objects, business rules, specifications, and validation helpers.
- `ApexPost.Application` – business services such as authentication, pricing, inventory, reports, dashboards, and transaction orchestration.
- `ApexPost.Infrastructure` – JSON repositories, seed data, notifications, and simulated external services.
- `ApexPost.Web` – ASP.NET Core MVC app, middleware, filters, controllers, and Razor views.
- `ApexPost.Tests` – xUnit tests covering the domain/business rules and integration behavior.

## Architecture overview

This solution follows a layered Clean Architecture style with ASP.NET Core MVC on top:

- Web depends on Application.
- Application depends on Domain.
- Infrastructure depends on Application and Domain.
- Controllers stay thin and delegate to service-layer logic.

## Prerequisites

- .NET 10 SDK
- Visual Studio 2026 or VS Code with C# support
- Local browser access to `http://localhost:5050`

## Run the application

From the repo root:

```bash
dotnet restore
cd ApexPost.Web
dotnet run
```

The app is configured to run on:

```text
http://localhost:5050
```

## Demo login accounts

The app seeds staff accounts in `SeedDataProvider.GetStaff()`. You can sign in with any active staff email, for example:

- `alicia.moore@apexpost.test` – Counter Clerk
- `carmen.lee@apexpost.test` – Branch Manager
- `darren.ross@apexpost.test` – Auditor
- `elena.park@apexpost.test` – Administrator

A login uses the email address only for this demo build.

## Configure the integration API key

Do not store secrets in source control. Use user secrets for the integration API key.

```bash
cd ApexPost.Web
dotnet user-secrets init
dotnet user-secrets set "Integration:ApiKey" "your-super-secret-key"
```

This is used for protected integration endpoints such as `/integration/parcel-status`.

## Features implemented

- Branch management and staff administration concepts
- Product catalogue and branch stock tracking
- Shipping and parcel validation
- Money order and identity verification rules
- Mixed basket transaction support for retail items and services
- Audit log recording via action filters
- Middleware for correlation IDs, request logging, rate limiting, maintenance mode, security headers, and IP restriction
- JSON-backed persistence without a database
- Seeded sample data for branches, staff, products, zones, and transactions
- Basic dashboard and reporting screens

## Business rules included

- Stock cannot go negative.
- Inactive staff cannot process transactions.
- A clerk can only process transactions at their assigned branch.
- Large money orders require successful identity verification.
- Parcel weight must stay within service-specific limits.
- Voids and refunds require branch manager role.
- Closed branches cannot accept transactions.

## Sample requests

Health check:

```bash
curl http://localhost:5050/health
```

Login (demo):

```bash
curl -c cookies.txt -X POST http://localhost:5050/login \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data "Email=alicia.moore@apexpost.test"
```

Protected partner endpoint example:

```bash
curl http://localhost:5050/integration/parcel-status \
  -H "X-API-Key: your-super-secret-key"
```

## Notes on persistence

This solution intentionally avoids a database and ORM. Data is persisted in JSON files behind repository abstractions so the business logic remains decoupled from storage implementation.

## Known demo assumptions

- Authentication is simplified for demo purposes.
- The app uses demo seed data and does not implement ASP.NET Core Identity.
- The integration API key is configured via user-secrets rather than checked into source control.

## License

This project is created as a learning and demonstration solution for the ApexPost Logistics lab scenario.
