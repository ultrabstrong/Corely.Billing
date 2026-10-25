# Corely.Billing.Demos.Subscription

The smallest host: a subscription is one capacity grant of three seats that never expires, and a members page opens only while it is enabled. Two Razor Pages and one class, `Membership.cs`, hold all of it.

## Run it

`.\Start-LocalStack.ps1 -Demo Subscription` from the repository root does every step below and opens the browser. By hand:

Needs SQL Server LocalDB. From the repository root:

```powershell
dotnet run --project Corely.Billing.DataAccessMigrations.Cli -- db create -p MsSql -c "Server=(localdb)\MSSQLLocalDB;Database=CorelyBillingDemoSubscription;Trusted_Connection=True;"
dotnet run --project Corely.Billing.Demos.Subscription
```

Open https://localhost:7111. Subscribe, open the members area, cancel, then subscribe again.

## What it uses

- **Subscribe**: `CreateGrantAsync` with `GrantKind.Capacity`, three seats, and `GrantConstants.NEVER_EXPIRES_UTC` as its end. A subscription ends when it lapses, not on a date.
- **Cancel**: `SetGrantDisabledAsync` disables the grant. That is the lapse.
- **Renew**: subscribing again enables the same grant, and its members come back with it.
- **The gate**: the members page opens only while `GetCapacityAsync` reads an active grant.
- **A member**: opening the members page registers "you" as a seat with `RegisterAsync`. A second visit is `AlreadyRegisteredError`, so the page still reads "1 of 3 seats in use".

## What it does not use, and why that is fine

- **Reservations and consumption**: a seat is held, not used up.
- **Payments**: whether the subscription is paid is the host's business; it disables or enables the grant to match.
- **Reports and time series**: seats in use is the whole question.

The tables for all of it are in the schema anyway, empty, and cost nothing.
