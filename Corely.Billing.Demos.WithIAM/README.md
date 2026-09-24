# Corely.Billing.Demos.WithIAM

A metered host signed in through Corely.IAM. The account comes from the IAM user context, and IAM permissions decide who may see and change grants and usage.

## Run it

Needs SQL Server LocalDB. Both libraries' schemas go into one database, each from its own tool. From the repository root:

```powershell
dotnet tool install --global Corely.IAM.DataAccessMigrations.Cli
corely-iam-db db create -p MsSql -c "Server=(localdb)\MSSQLLocalDB;Database=CorelyBillingDemoWithIam;Trusted_Connection=True;"
dotnet run --project Corely.Billing.DataAccessMigrations.Cli -- db migrate -p MsSql -c "Server=(localdb)\MSSQLLocalDB;Database=CorelyBillingDemoWithIam;Trusted_Connection=True;"
dotnet run --project Corely.Billing.Demos.WithIAM -- --seed
dotnet run --project Corely.Billing.Demos.WithIAM
```

The seed creates account Acme with owner `olivia` and member `bobby`, password `Test1234`, two grants, and some usage. Open https://localhost:7112.

Sign in as `olivia`: she owns the account, so she sees the usage and can manage grants. Sign in as `bobby`: he is a member with no roles, so every panel says he is not allowed.

`appsettings.Development.json` holds a committed system key. It protects nothing but local demo data.

## What to look at

- `IamBillingAccountAccessor.cs` - `CurrentAccount.Id` is the billing account; `CanManageGrantsAsync` asks IAM for Update on `grants`.
- `Authorization/` - two decorators passed to `BillingOptions.DecorateServices`, checking IAM permissions on the `grants` and `usage` resource types. `Program.cs` registers both types with IAM, or IAM would reject permissions on them.
- `Program.cs` - the quota service is not decorated. Work that consumes quota runs as the system, not as whoever is watching the chart.
- `Components/Layout/DemoLayout.razor` - guards the library's routed pages, which carry no `[Authorize]` of their own.
