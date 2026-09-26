# Corely.Billing.Web.IAM Documentation

Corely.IAM.Web for Corely.Billing.Web. The Billing components take the account from the signed-in user, and each grant action is gated by IAM's `PermissionView`, so a role sees exactly the controls its permissions allow.

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
| `IBillingAccountAccessor` | `UserContext.CurrentAccount?.Id` of the signed-in user |
| `IGrantActionGate` | IAM's `PermissionView` around each grant action, on `grant` |

It calls `AddBillingWeb` itself.

## What Each Role Sees

| Permissions on `grant` | Grant list | Editor |
|------------------------|------------|--------|
| Read | View link on each row | Read-only |
| Read, Update | Edit on each row | Editable |
| Read, Update, Create, Delete | New grant, Edit and Delete | Editable, and new grants |
| None | "You are not allowed to view grants." | "You are not allowed to view this grant." |

Edit and Delete check the row's grant id, so a permission on one grant shows its controls and no others.

## Notes

- The gate and `PermissionView` read the user from the circuit's scope. Billing's components share that scope; a component resolving services in a scope of its own would see no user and hide everything.
- Hiding a control is presentation. The decorators from Corely.Billing.IAM refuse the call whatever the screen shows.

Working example: `Corely.Billing.Demos.WithIAM`.
