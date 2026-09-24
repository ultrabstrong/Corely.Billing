# Corely.Billing.Demos.Subscription

The smallest host: a year's subscription is one unlimited grant, and a members page opens only while it is live. Two Razor Pages and one class, `Membership.cs`, hold all of it.

## Run it

Needs SQL Server LocalDB. From the repository root:

```powershell
dotnet run --project Corely.Billing.DataAccessMigrations.Cli -- db create -p MsSql -c "Server=(localdb)\MSSQLLocalDB;Database=CorelyBillingDemoSubscription;Trusted_Connection=True;"
dotnet run --project Corely.Billing.Demos.Subscription
```

Open https://localhost:7111. Subscribe, open the members area a few times, then cancel.

## What it uses

- **Subscribe** - `CreateGrantAsync` with `Quantity: null` for a year.
- **Cancel** - `UpdateGrantAsync` moves `ValidToUtc` to now.
- **The gate** - `GetAvailabilityAsync` must return `Available`. `Unknown`, when the database cannot be reached, is a refusal: a subscription gate fails closed.
- **Visit count** - each visit reserves and settles 1 under its own operation scope, and `GetGrantConsumptionTotalsAsync` on the term's grant is "visited N times this term".

## What it does not use, and why that is fine

- **Limited quantities, overdraft, grant-edge splitting** - an unlimited grant is never short.
- **Reservation TTL** - every reserve is settled in the same request.
- **Reports and time series** - one total is the whole question.
- **Payments and renewals** - a term is a grant with dates. What creates the next one is the host's business.

The tables for all of it are in the schema anyway, empty, and cost nothing.
