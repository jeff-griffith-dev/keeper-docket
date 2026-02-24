# Docket — Azure Stack

.NET 9 / ASP.NET Core Minimal API / Entity Framework Core  
SQLite for development · Azure SQL for production

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- Visual Studio 2022 (17.8+) or VS Code with C# Dev Kit

---

## First-time setup

```powershell
# From the solution root (where Docket.sln lives)

# Restore packages
dotnet restore

# Install the EF Core tools if you haven't already
dotnet tool install --global dotnet-ef

# Create the initial migration
dotnet ef migrations add InitialCreate `
  --project src\Docket.Infrastructure `
  --startup-project src\Docket.Api `
  --output-dir Migrations

# Run the API — migrations are applied automatically on startup in Development
dotnet run --project src\Docket.Api
```

The API starts at `https://localhost:5001` (or `http://localhost:5000`).  
Scalar API reference is available at `https://localhost:5001/scalar/v1`.

---

## Project structure

```
Docket.Domain/          Entities, enums, exceptions, domain logic
                        No EF dependency. Business rules live here.

Docket.Infrastructure/  EF Core DbContext, entity configuration, migrations
                        Knows about both SQLite and Azure SQL.

Docket.Api/             ASP.NET Core Minimal API
                        Endpoint groups, DTOs, middleware, Program.cs

Docket.Tests/           xUnit integration tests
                        Each test class gets an isolated in-memory SQLite DB.
```

---

## Running tests

```powershell
dotnet test
```

Integration tests use `WebApplicationFactory<Program>` with an isolated
in-memory SQLite database per test class. No external dependencies required.

---

## Database

**Development:** SQLite file at `src/Docket.Api/docket-dev.db`  
Migrations run automatically on startup. The dev stub user
(`dev@docket.local`, ID `00000000-0000-0000-0000-000000000001`) is seeded
automatically.

**Production:** Set the `DefaultConnection` connection string to an Azure SQL
connection string. The application will use SQL Server automatically when
`ASPNETCORE_ENVIRONMENT` is not `Development`.

```json
// appsettings.Production.json (or environment variable)
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=docket;..."
  }
}
```

To generate migrations after model changes:

```powershell
dotnet ef migrations add <MigrationName> `
  --project src\Docket.Infrastructure `
  --startup-project src\Docket.Api `
  --output-dir Migrations
```

---

## Authentication

**v1:** Stub authentication. All requests are treated as the dev user
(`StubCurrentUserService`). No token required.

**Production path:** Replace the `ICurrentUserService` registration in
`Program.cs` with a `JwtCurrentUserService` implementation. The interface
contract is stable — no endpoint code changes required.

```csharp
// Current (stub)
builder.Services.AddScoped<ICurrentUserService, StubCurrentUserService>();

// Future (JWT)
builder.Services.AddScoped<ICurrentUserService, JwtCurrentUserService>();
```

---

## Error responses

All errors return RFC 7807 Problem Details with a Docket-specific `title`
field containing the machine-readable error code:

```json
{
  "status": 422,
  "title": "MINUTES_FINALIZED",
  "detail": "Minutes abc123 has been finalized and cannot be modified."
}
```

See `docs/01-spec/docket-feature-spec.md` — Appendix: Error Codes for the
full list.
