# Corely.Billing.Demos.Portal

The `Corely.Billing.Web` components against one demo account, with nothing else in the way: no sign-in, and a fixed account id from a two-line `IBillingAccountAccessor`.

## Run it

`.\Start-LocalStack.ps1 -Demo Portal` from the repository root does every step below and opens the browser. By hand:

Needs SQL Server LocalDB (installed with Visual Studio). From the repository root:

```powershell
dotnet run --project Corely.Billing.DataAccessMigrations.Cli -- db create -p MsSql -c "Server=(localdb)\MSSQLLocalDB;Database=CorelyBillingDemoPortal;Trusted_Connection=True;"
dotnet run --project Corely.Billing.Demos.Portal -- --seed
dotnet run --project Corely.Billing.Demos.Portal
```

The first command creates the schema with the same `corely-billing-db` tool a deployment uses. `--seed` writes a grant of every kind: consumable token grants that are expired and overdrawn, active, active and expiring soon, and upcoming, plus unlimited embeddings and 1,000 images; five team seats with three members registered; 60 extra days of log retention; and fine tuning as a feature. It also writes a pending request for 2,000 more images, and six months of model usage, so the charts are worth looking at. Rerunning it does nothing once the account has grants. Open https://localhost:7110.

## What to look at

- `/`: a page the demo composes itself from `UsageDashboard`. "Simulate a chat reply" holds an estimated 1,000 tokens and settles what the reply used; "Simulate embeddings" and "Simulate images" hold and settle an exact count, since both are known before the request is sent. Each runs under a fresh operation scope, then refreshes the dashboard.
- `/usage`: pick a view (Stacked, Remaining, Burn-up, Share) and a unit, and Export the zip.
- `/grants`: the pending request above the grants, with Approve and Decline; `/grants/request` to ask for another. Each grant shows its own state: a meter for a balance, seats in use, the days added, or enabled.
- `/grants/{id}` on the team seats grant: the members holding seats, with Release and Move.
- `/grants`, `/grants/new`, `/grants/request`, `/grants/{id}`, `/usage`: the demo's own page shells in `Components/Pages`, each around one of the library's components, with the paths in `PortalRoutes`.
- The moon button switches `data-bs-theme`; the components and charts follow.
- `DemoSeed.cs`: backdates usage by swapping in a `TimeProvider` the seed moves day by day.
