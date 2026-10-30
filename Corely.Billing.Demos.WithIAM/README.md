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

The seed creates account Acme with five users, password `Test1234`, two grants, some usage, and a pending grant request from `olivia`. Open https://localhost:7112. In Development the sign-in page has a "Demo only: quick sign in" dropdown of the five, from `Pages/Authentication/_SignInAssist.cshtml`; picking one signs in as them.

| User | Permissions | Sees |
|------|-------------|------|
| `olivia` | Owner: Read on `grant`, `consumption` and `quota`, Read and Create on `grant_request`, Read and Execute on `registration`. Client, as below | Usage and grants, read-only; her pending request, and Request a grant once nothing is pending |
| `carla` | Read and Update on `grant`, Read on `consumption` | Usage, and Edit on each row; no New grant, no Delete, no requests |
| `chloe` | Client: Read on `consumption`, Read and Execute on `quota` | Usage alone: no Grants link, no Grant filter, column or chart views, and an export without `grants.csv` |
| `priya` | Provisioner: Read and Create on `grant`, Read and Update on `grant_request`, Read on `consumption` | New grant, and Approve and Decline on each pending request; no Request a grant |
| `bobby` | A member with no roles | "You are not allowed to…" on every panel |

`carla` is the proof that each grant action is gated on its own permission, not on one "can manage" flag. Her `grant` permission is created by the seed under system context: an owner holds only Read on `grant`, and IAM refuses to hand out what the caller does not hold, so nobody signed in can give it to her. That is also why `olivia` sees no New grant, Edit or Delete.

`chloe` is the customer of a metered product: usage, with no trace of the grants behind it. The "Client" role holds the owner's system defined `consumption` permission, a Read and Execute permission on `quota` the seed creates under system context, because an owner holds only Read there, and nothing on `grant`. The dashboard decides this from the grant list coming back unauthorized, so one page serves both `chloe` and `olivia`. `/grants` typed by hand is still refused.

`appsettings.Development.json` holds a committed system key. It protects nothing but local demo data.

## What to look at

- `Program.cs`: three calls: `RegisterBillingResourceTypes()`, `UseCorelyIamPermissions()` and `AddBillingWebIam()`. No accessor or decorators of its own.
- `priya` approving `olivia`'s request turns it into a grant for a year in the same save; declining asks for an optional reason. `priya` stands in for the platform admin a real host would have.
- `DemoSeed.cs`: `carla`'s, `chloe`'s and `priya`'s roles and the two grants, created under `AuthenticateAsSystem`, the way a host provisions what its users cannot.
- "Ask the assistant" holds an estimated 1,000 tokens and settles what the reply used, as the signed-in user, so it needs Execute on `quota`: `olivia` and `chloe` have it through the Client role, `carla` does not. A host whose work runs on the platform's side meters as the system instead, and no user needs it.
- `Components/Pages`: the page shells at account paths (`/accounts/{id}/grants`, `/accounts/{id}/usage`, paths in `DemoRoutes`), each guarded by `[Authorize]` and Corely.IAM.Web's `AccountScope`, loading for the signed in account and using the route's only for links.
- `Components/Layout/DemoLayout.razor`: shows the Grants link only with Read on `grant`, through `PermissionView`.
