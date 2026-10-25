# Demos

Three runnable hosts, each showing a different way to use Corely.Billing, plus one shared asset project. All run on SQL Server LocalDB with the schema created by `corely-billing-db`, the same way a deployment does.

| Project | Shows | Runs at |
|---------|-------|---------|
| [`Corely.Billing.Demos.Portal`](../../Corely.Billing.Demos.Portal/README.md) | The `Corely.Billing.Web` components on one account | https://localhost:7110 |
| [`Corely.Billing.Demos.Subscription`](../../Corely.Billing.Demos.Subscription/README.md) | The smallest host: a subscription as one capacity grant | https://localhost:7111 |
| [`Corely.Billing.Demos.WithIAM`](../../Corely.Billing.Demos.WithIAM/README.md) | A metered host signed in and authorized through [Corely.IAM](https://github.com/ultrabstrong/Corely.IAM) | https://localhost:7112 |
| `Corely.Billing.Demos.Bootstrap` | Not an app: Bootstrap and Bootstrap Icons, served once to all three | none |

## Portal

The web components with nothing else in the way: no sign-in, and a fixed account id from a two-line `IBillingAccountAccessor`. It plays an AI product billed in tokens and images. `--seed` writes a grant of every kind and six months of model usage:
- an expired token grant that was overdrawn
- two overlapping active grants, one expiring soon
- an upcoming renewal
- an unlimited grant for embeddings
- an image grant, in a second unit
- a capacity of five team members, three of them registered
- 60 extra days of log retention, an allowance
- fine tuning, a feature
- a pending request for more images, to approve or decline on `/grants`

- **Home**: `UsageDashboard` composed into the demo's own page, with buttons that simulate a chat reply (an estimated hold, settled to the tokens used) an embedding request and an image request (exact holds)
- **`/grants`, `/usage`**: the demo's own page shells, each around one of the library's components
- **Theme button**: switches `data-bs-theme`; the components and charts follow

## Subscription

A subscription is one capacity grant of three members that never expires. Two Razor Pages and one class hold all of it.

- **Subscribe** creates the grant; **Cancel** disables it, which is the lapse; subscribing again enables it
- **The members page** opens only while `GetCapacityAsync` reads an active grant
- **Each visit** registers "you", once, and the page shows "1 of 3 members in use"

It uses no reservations, no consumption and no reports. See [Usage Shapes](usage-shapes.md).

## WithIAM

An AI assistant billed in tokens, inside a Corely.IAM app, wired with [Corely.Billing.IAM](../../Corely.Billing.IAM/Docs/index.md) and [Corely.Billing.Web.IAM](../../Corely.Billing.Web.IAM/Docs/index.md). The account comes from the signed-in user's context, and IAM permissions decide what each user sees and may change, one action at a time. Both libraries' schemas share one database, each created by its own tool.

- **`olivia`** owns the account: she sees usage and grants, and runs the assistant, but cannot create or change grants. The seed provisions those under system context
- **`carla`** may read and update grants and read consumption: no New grant, no Delete, and Edit on each row
- **`chloe`** is a client: usage alone, no grants
- **`priya`** provisions: creates grants, and approves or declines the grant requests owners send. The seed leaves one pending request from `olivia`
- **`bobby`** is a member with no roles: every panel says he is not allowed

The password for all five is `Test1234`.

## Running

Each demo's README has the exact commands. The pattern is the same for all three:

```powershell
dotnet run --project Corely.Billing.DataAccessMigrations.Cli -- db create -p MsSql -c "<connection string>"
dotnet run --project Corely.Billing.Demos.Portal -- --seed
dotnet run --project Corely.Billing.Demos.Portal
```

The portal and WithIAM take `--seed`. The subscription demo starts empty.
