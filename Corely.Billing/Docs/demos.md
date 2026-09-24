# Demos

Three runnable hosts, each showing a different way to use Corely.Billing, plus one shared asset project. All run on SQL Server LocalDB with the schema created by `corely-billing-db`, the same way a deployment does.

| Project | Shows | Runs at |
|---------|-------|---------|
| [`Corely.Billing.Demos.Portal`](../../Corely.Billing.Demos.Portal/README.md) | The `Corely.Billing.Web` components on one account | https://localhost:7110 |
| [`Corely.Billing.Demos.Subscription`](../../Corely.Billing.Demos.Subscription/README.md) | The smallest host: a subscription as one unlimited grant | https://localhost:7111 |
| [`Corely.Billing.Demos.WithIAM`](../../Corely.Billing.Demos.WithIAM/README.md) | A metered host signed in and authorized through Corely.IAM | https://localhost:7112 |
| `Corely.Billing.Demos.Bootstrap` | Not an app: Bootstrap and Bootstrap Icons, served once to all three | — |

## Portal

The web components with nothing else in the way: no sign-in, and a fixed account id from a two-line `IBillingAccountAccessor`. `--seed` writes five grants and six months of usage:
- an expired grant that was overdrawn
- two overlapping active grants, one expiring soon
- an upcoming renewal
- an unlimited grant

- **Home** — `UsageDashboard` composed into the demo's own page, with buttons that reserve and settle a random quantity
- **`/grants`, `/usage`** — the library's routed pages, present because the host adds its assembly to the router
- **Theme button** — switches `data-bs-theme`; the components and charts follow

## Subscription

A year's subscription is one grant with a null `Quantity`, which is unlimited. Two Razor Pages and one class hold all of it.

- **Subscribe** creates the grant; **Cancel** moves its `ValidToUtc` to now
- **The members page** opens only while `GetAvailabilityAsync` returns `Available`, so it fails closed
- **Each visit** is a reserve and settle of one, and the page shows "visited N times this term"

It uses no reservations beyond that, no limited quantities, and no reports. See [Usage Shapes](usage-shapes.md).

## WithIAM

Billing inside a Corely.IAM app. The account comes from the signed-in user's context, and IAM permissions decide who sees and changes grants and usage. The authorization runs through `BillingOptions.DecorateServices`. Both libraries' schemas share one database, each created by its own tool.

- **`olivia`** owns the account: she sees usage and manages grants
- **`bobby`** is a member with no roles: every panel says he is not allowed

The password for both is `Test1234`.

## Running

Each demo's README has the exact commands. The pattern is the same for all three:

```powershell
dotnet run --project Corely.Billing.DataAccessMigrations.Cli -- db create -p MsSql -c "<connection string>"
dotnet run --project Corely.Billing.Demos.Portal -- --seed
dotnet run --project Corely.Billing.Demos.Portal
```

The portal and WithIAM take `--seed`. The subscription demo starts empty.
