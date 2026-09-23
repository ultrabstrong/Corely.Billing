# Step-by-Step Setup

Takes a host from nothing to a reserved and settled charge. Each step shows the minimum code.

## 1) Install the Packages

```bash
dotnet add package Corely.Billing
dotnet tool install --global Corely.Billing.DataAccessMigrations.Cli
```

## 2) Create the Schema

The library never applies migrations at runtime. The tool ships both providers' migrations.

```bash
corely-billing-db db create -p MsSql -c "Server=(localdb)\MSSQLLocalDB;Database=MyApp;Trusted_Connection=True;"
```

Billing records its migrations in `__CorelyBillingMigrationsHistory`, so it can share a database with Corely.IAM and the host's own contexts. See the [Migration CLI](../../Corely.Billing.DataAccessMigrations.Cli/Docs/index.md) docs for details.

## 3) Choose a Database Provider

Create an `IEFConfiguration` for your database from the `Corely.DataAccess` base classes.

```csharp
public class MsSqlEFConfiguration(string connectionString)
    : EFMsSqlConfigurationBase(connectionString)
{
    public override void Configure(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseSqlServer(connectionString);
}
```

| Provider | Base Class | Connection String Example |
|----------|-----------|---------------------------|
| SQL Server | `EFMsSqlConfigurationBase` | `Server=(localdb)\MSSQLLocalDB;Database=MyApp;Trusted_Connection=True;` |
| MySQL | `EFMySqlConfigurationBase` | `Server=localhost;Database=my_app;Uid=root;Pwd=password;` |

The factory serves Billing's DbContext only. If the host has DbContexts of its own that take an
`IEFConfiguration`, register one for them — Billing does not.

## 4) Configure BillingOptions

Register every operation and unit the host bills for. Nothing can be granted or consumed without them.

```csharp
var options = BillingOptions
    .Create(builder.Configuration, _ => new MsSqlEFConfiguration(connectionString))
    .RegisterOperation("document_extraction", "Document Extraction")
    .RegisterUnit("page", "page");
```

## 5) Register Services

```csharp
builder.Services.AddBillingServices(options);
```

## 6) Grant Quota

```csharp
var result = await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, extraction, page, Quantity: 500,
    ValidFromUtc: now, ValidToUtc: now.AddMonths(1)));
```

## 7) Reserve, Work, Settle

Every quota call runs inside an operation scope. The scope names the unit of work and must be the same on every retry of it.

```csharp
using var scope = accessor.BeginScope(new OperationContext(correlationId, $"job:{jobId}/step:extract"));

var reserved = await quotaService.ReserveAsync(
    new ReserveQuotaRequest(accountId, extraction, page, Quantity: 1, Provider: "mistral"));
if (reserved.ResultCode != ReserveQuotaResultCode.Success)
    return;

var pages = await ExtractAsync(document);
await quotaService.SettleAsync(new SettleQuotaRequest(accountId, extraction, page, pages));
```

On a terminal failure, give the hold back with `ReleaseAsync` instead of settling. See the [Reservations](reservations.md) docs for the full lifecycle.
