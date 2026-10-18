# Corely.Billing.IAM Documentation

[Corely.IAM](https://github.com/ultrabstrong/Corely.IAM) permissions for Corely.Billing. Registers the `grant`, `grant_request`, `consumption` and `quota` resource types with IAM, and decorates the four Billing services the way Corely.IAM decorates its own: the caller's account first, then CRUDX on the resource type.

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
| `BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE` | `grant_request` |
| `BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE` | `consumption` |
| `BillingResourceTypes.QUOTA_RESOURCE_TYPE` | `quota` |

Registered, they appear in the IAM.Web permission form's resource type list.

## What Account Owners Get

`RegisterBillingResourceTypes()` declares these as the Owner role's actions, so every new account's
owner holds them:

| Type | Owner actions | Why |
|------|---------------|-----|
| `grant` | Read | Owners see their quota but cannot give themselves more. Grants are provisioned by the host, under system context, or by approving a request |
| `grant_request` | Read, Create | Owners ask for more and see what they asked. Deciding (Update) is held by no owner |
| `consumption` | Read | Owners see their usage |
| `quota` | Read, Execute | Owners can run work that consumes their own quota |

Because IAM refuses to hand out what the caller does not hold, an owner also cannot create a
`grant: Create` permission or give it to anyone through a role. A person who provisions grants for
other accounts is the host's own concern: a host operation that runs under system context, or a role
the host gives Create on `grant` and Update on `grant_request`.

IAM adds owner defaults for a newly registered type to existing accounts too, so accounts created
before `grant_request` existed gain Read and Create on it.

## Permissions

Every method first checks `HasAccountContext` for the account it names. The system context passes both checks, so a pipeline running as the system is unaffected.

| Service method | Permission |
|----------------|------------|
| `CreateGrantAsync` | Create on `grant` |
| `GetGrantAsync` | Read on `grant`, that grant |
| `ListGrantsAsync` | Read on `grant`, then only the grants the caller may read |
| `UpdateGrantAsync`, `SetGrantDisabledAsync` | Update on `grant`, that grant |
| `DeleteGrantAsync` | Delete on `grant`, that grant |
| `RequestGrantAsync` | Create on `grant_request` |
| `GetGrantRequestAsync` | Read on `grant_request`, that request |
| `ListGrantRequestsAsync` | Read on `grant_request`, then only the requests the caller may read |
| `ApproveGrantRequestAsync` | Update on `grant_request`, that request, **and** Create on `grant` |
| `DeclineGrantRequestAsync` | Update on `grant_request`, that request |
| Every `IConsumptionService` read | Read on `consumption` |
| `GetAvailabilityAsync` | Read on `quota` |
| `ReserveAsync`, `SettleAsync`, `ReleaseAsync` | Execute on `quota` |

A request's `RequestedByUserId` and a decision's `DecidedByUserId` are replaced with the signed-in user's id, whatever the caller passed, so nobody can record a request or decision as someone else's. Only the system context keeps the id it was given, so a host's background work can act for a named user.

Approving creates a grant, so it also needs what creating one needs; otherwise a role that cannot create grants could create them by approving.

A denial returns the service's `UnauthorizedError` result, worded as IAM words its own: `Unauthorized to delete grant {grantId}`. A denied `GetAvailabilityAsync` returns `QuotaAvailability.Unauthorized`.

## Auditing

`UseCorelyIamPermissions()` also gives each service an audit decorator, so every call is recorded in
[Corely.IAM's audit log](https://github.com/ultrabstrong/Corely.IAM/blob/master/Corely.IAM/Docs/auditing.md)
with the same action and resource type its permission uses, in the account it names. What is actually
written follows the platform's and the account's audit settings. Grant and request changes (Create,
Update, Delete) are recorded by default; reads and quota calls (Read, Execute) only where an account turns them
on, which keeps busy consumption paths free of writes.

| Service | Recorded as |
|---------|-------------|
| `IGrantService` | The action above on `grant`, with the grant's id (a created grant's id comes from the result) |
| `IGrantRequestService` | Create (ask), Read, or Update (approve, decline) on `grant_request`, with the request's id |
| `IConsumptionService` | Read on `consumption` |
| `IQuotaService` | Read (availability) or Execute (reserve, settle, release) on `quota` |

Order: Billing's telemetry, then audit, then authorization, then the service, so a refused call is
recorded with its refusal code.

## Notes

- The decorators sit inside Billing's telemetry decorators, so a denied call is still logged.
- One permission covers reserve, settle and release, so a caller can never hold quota it cannot settle or release.
- A user who consumes quota interactively needs Execute on `quota`; one who only watches usage needs Read on `consumption`.
