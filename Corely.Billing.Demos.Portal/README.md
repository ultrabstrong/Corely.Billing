# Corely.Billing.Demos.Portal

The `Corely.Billing.Web` components against one demo account, with nothing else in the way: no sign-in, and a fixed account id from a two-line `IBillingAccountAccessor`.

## Run it

Needs SQL Server LocalDB (installed with Visual Studio). From the repository root:

```powershell
dotnet run --project Corely.Billing.DataAccessMigrations.Cli -- db create -p MsSql -c "Server=(localdb)\MSSQLLocalDB;Database=CorelyBillingDemoPortal;Trusted_Connection=True;"
dotnet run --project Corely.Billing.Demos.Portal -- --seed
dotnet run --project Corely.Billing.Demos.Portal
```

The first command creates the schema with the same `corely-billing-db` tool a deployment uses. `--seed` writes five grants (token grants that are expired and overdrawn, active, active and expiring soon, and upcoming, plus unlimited embeddings) and six months of model usage, so the charts are worth looking at. Rerunning it does nothing once the account has grants. Open https://localhost:7110.

## What to look at

- `/`: a page the demo composes itself from `UsageDashboard`. "Simulate a chat reply" holds an estimated 1,000 tokens and settles what the reply used; "Simulate embeddings" holds and settles an exact count, since text to embed is counted before it is sent. Each runs under a fresh operation scope, then refreshes the dashboard.
- `/grants`, `/grants/new`, `/grants/{id}`, `/usage`: the library's routed pages, present because `Program.cs` and `Routes.razor` add its assembly.
- The moon button switches `data-bs-theme`; the components and charts follow.
- `DemoSeed.cs`: backdates usage by swapping in a `TimeProvider` the seed moves day by day.
