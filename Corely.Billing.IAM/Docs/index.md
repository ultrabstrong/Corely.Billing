# Corely.Billing.IAM Documentation

[Corely.IAM](https://github.com/ultrabstrong/Corely.IAM) permissions for Corely.Billing. Registers the `grant`, `consumption` and `quota` resource types with IAM, and decorates the three Billing services the way Corely.IAM decorates its own: the caller's account first, then CRUDX on the resource type.

- **One call each side**: `RegisterBillingResourceTypes()` on `IAMOptions`, `UseCorelyIamPermissions()` on `BillingOptions`
- **IAM's own mechanisms**: `IAuthorizationProvider`, `RegisterResourceType` and `UnauthorizedError` results, nothing new
- **Account first**: a caller authorized for grants in one account cannot reach another's by passing its id
- **Per-grant permissions**: get, update and delete check the grant's id; lists return only the grants the caller may read
- **Quota on quota alone**: nobody assigning permissions needs to know that quota reads grants and writes consumption

## Setup

```csharp
builder.Services.AddIAMServices(iamOptions.RegisterBillingResourceTypes());
builder.Services.AddBillingServices(billingOptions.UseCorelyIamPermissions());
```

Call `AddIAMServices` first. `UseCorelyIamPermissions()` throws, naming `AddIAMServices`, when IAM's `IAuthorizationProvider` is not registered yet.

A host with Blazor adds [Corely.Billing.Web.IAM](../../Corely.Billing.Web.IAM/Docs/index.md) for the matching UI.

## Resource Types

| Constant | Value |
|----------|-------|
| `BillingResourceTypes.GRANT_RESOURCE_TYPE` | `grant` |
| `BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE` | `consumption` |
| `BillingResourceTypes.QUOTA_RESOURCE_TYPE` | `quota` |

Registered, they appear in the IAM.Web permission form's resource type list.

## What Account Owners Get

`RegisterBillingResourceTypes()` declares these as the Owner role's actions, so every new account's
owner holds them:

| Type | Owner actions | Why |
|------|---------------|-----|
| `grant` | Read | Owners see their quota but cannot give themselves more. Grants are provisioned by the host, under system context |
| `consumption` | Read | Owners see their usage |
| `quota` | Read, Execute | Owners can run work that consumes their own quota |

Because IAM refuses to hand out what the caller does not hold, an owner also cannot create a
`grant: Create` permission or give it to anyone through a role. A person who provisions grants for
other accounts is the host's own concern: a host operation that runs under system context.

## Permissions

Every method first checks `HasAccountContext` for the account it names. The system context passes both checks, so a pipeline running as the system is unaffected.

| Service method | Permission |
|----------------|------------|
| `CreateGrantAsync` | Create on `grant` |
| `GetGrantAsync` | Read on `grant`, that grant |
| `ListGrantsAsync` | Read on `grant`, then only the grants the caller may read |
| `UpdateGrantAsync` | Update on `grant`, that grant |
| `DeleteGrantAsync` | Delete on `grant`, that grant |
| Every `IConsumptionService` read | Read on `consumption` |
| `GetAvailabilityAsync` | Read on `quota` |
| `ReserveAsync`, `SettleAsync`, `ReleaseAsync` | Execute on `quota` |

A denial returns the service's `UnauthorizedError` result, worded as IAM words its own: `Unauthorized to delete grant {grantId}`. A denied `GetAvailabilityAsync` returns `QuotaAvailability.Unauthorized`.

## Notes

- The decorators sit inside Billing's telemetry decorators, so a denied call is still logged.
- One permission covers reserve, settle and release, so a caller can never hold quota it cannot settle or release.
- A user who consumes quota interactively needs Execute on `quota`; one who only watches usage needs Read on `consumption`.
