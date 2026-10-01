# Corely.Billing.Demos.WithIAM

A metered host signed in through [Corely.IAM](https://github.com/ultrabstrong/Corely.IAM), wired with [Corely.Billing.IAM](../Corely.Billing.IAM/Docs/index.md) and [Corely.Billing.Web.IAM](../Corely.Billing.Web.IAM/Docs/index.md). The account comes from the IAM user context, and IAM permissions decide what each user sees and may change.

## Run it

`.\Start-LocalStack.ps1 -Demo WithIAM` from the repository root does every step below and opens the browser. By hand:

Needs SQL Server LocalDB. Both libraries' schemas go into one database, each from its own tool. From the repository root:

```powershell
dotnet tool install --global Corely.IAM.DataAccessMigrations.Cli
corely-iam-db db create -p MsSql -c "Server=(localdb)\MSSQLLocalDB;Database=CorelyBillingDemoWithIam;Trusted_Connection=True;"
dotnet run --project Corely.Billing.DataAccessMigrations.Cli -- db migrate -p MsSql -c "Server=(localdb)\MSSQLLocalDB;Database=CorelyBillingDemoWithIam;Trusted_Connection=True;"
dotnet run --project Corely.Billing.Demos.WithIAM -- --seed
dotnet run --project Corely.Billing.Demos.WithIAM
```

The seed creates account Acme with three users, password `Test1234`, two grants, and some usage. Open https://localhost:7112.

| User | Permissions | Sees |
|------|-------------|------|
| `olivia` | Owner, every permission | Usage, and New grant, Edit and Delete |
| `carla` | Read and Update on `grant`, Read on `consumption` | Usage, and Edit on each row; no New grant, no Delete |
| `bobby` | A member with no roles | "You are not allowed to…" on every panel |

`carla` is the proof that each grant action is gated on its own permission, not on one "can manage" flag.

`appsettings.Development.json` holds a committed system key. It protects nothing but local demo data.

## What to look at

- `Program.cs`: three calls: `RegisterBillingResourceTypes()`, `UseCorelyIamPermissions()` and `AddBillingWebIam()`. No accessor or decorators of its own.
- `DemoSeed.cs`: `carla`'s role, built from IAM's own registration service.
- "Ask the assistant" holds an estimated 1,000 tokens and settles what the reply used, as the signed-in user, so it needs Execute on `quota`: `olivia` has it, `carla` does not.
- `Components/Layout/DemoLayout.razor`: guards the library's routed pages, which carry no `[Authorize]` of their own.
