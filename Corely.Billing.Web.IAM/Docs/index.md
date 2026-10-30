# Corely.Billing.Web.IAM Documentation

[Corely.IAM.Web](https://github.com/ultrabstrong/Corely.IAM/blob/master/Corely.IAM.Web/Docs/index.md) for Corely.Billing.Web. The Billing components take the account from the signed-in user, and each grant action is gated by IAM's `PermissionView`, so a role sees exactly the controls its permissions allow.

## Setup

```csharp
builder.Services.AddIAMServices(iamOptions.RegisterBillingResourceTypes());
builder.Services.AddBillingServices(billingOptions.UseCorelyIamPermissions());
builder.Services.AddBillingWebIam();
```

Nothing else: no accessor, no decorators, no `PermissionView` wrappers of the host's own. The first two lines come from [Corely.Billing.IAM](../../Corely.Billing.IAM/Docs/index.md).

## What AddBillingWebIam() Registers

| Service | Does |
|---------|------|
| `IBillingAccountAccessor` | `UserContext.CurrentAccount?.Id` of the signed-in user, and `UserContext.User?.Id` as the user |
| `IGrantActionGate` | IAM's `PermissionView` around each grant action, mapped below |

| Grant action | Permission |
|--------------|------------|
| `Create`, `Update`, `Delete` | The same action on `grant` |
| `Request` | Create on `grant_request` |
| `Decide` | Update on `grant_request` for that request, inside Create on `grant`: the pair approving is authorized against |
| `ManageRegistrations` | Execute on `registration`: releasing and moving registrations |

It calls `AddBillingWeb` itself.

## API

`MapBillingApiEndpoints()` serves quota reads, registrations, grants and consumption over HTTP to a
caller holding an IAM token, an API key's or a user's. `MapBillingMeteringEndpoints()` adds reserve,
settle and release for a host that meters over HTTP. See [Billing API](api.md).

## What Each Role Sees

| Permissions on `grant` | Grant list | Editor |
|------------------------|------------|--------|
| Read | View link on each row | Read-only |
| Read, Update | Edit on each row | Editable |
| Read, Update, Create, Delete | New grant, Edit and Delete | Editable, and new grants |
| None | "You are not allowed to view grants." | "You are not allowed to view this grant." |

Edit and Delete check the row's grant id, so a permission on one grant shows its controls and no others.

| Permissions on `grant_request` | Grant list |
|--------------------------------|------------|
| None | No request rows, and no Request a grant |
| Read, Create (an owner's default) | Pending requests, and Request a grant |
| Read, Update, with Create on `grant` | Approve and Decline on each pending request |

Decline shows under the same gate as Approve. The service lets a caller with Update on `grant_request` and no Create on `grant` decline through the API; the screen does not offer it.

## Notes

- Every Billing component owns its DI scope. The accessor signs that scope in through `IBlazorUserContextAccessor` before reading the account, so the decorators from Corely.Billing.IAM see the user; `PermissionView` signs in its own scope the same way.
- Hiding a control is presentation. The decorators from Corely.Billing.IAM refuse the call whatever the screen shows.

Working example: `Corely.Billing.Demos.WithIAM`.
