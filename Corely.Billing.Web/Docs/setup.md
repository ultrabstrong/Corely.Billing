# Setup

Adds the components to a Blazor Server host that already registers Corely.Billing.

## 1) Install the Package

```bash
dotnet add package Corely.Billing.Web
```

## 2) Tell the Components Which Account

Implement `IBillingAccountAccessor`.

```csharp
internal sealed class MyAccountAccessor(IMySession session) : IBillingAccountAccessor
{
    public Task<Guid?> GetAccountIdAsync() => Task.FromResult(session.AccountId);
}
```

A Corely.IAM host skips this step and the next: [Corely.Billing.Web.IAM](../../Corely.Billing.Web.IAM/Docs/index.md)'s `AddBillingWebIam()` registers an accessor that reads the signed-in user's account, and gates each action with IAM's `PermissionView`.

## 3) Register Services

```csharp
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddBillingServices(billingOptions);
builder.Services.AddBillingWeb<MyAccountAccessor>();
```

| Registered | Lifetime | Purpose |
|------------|----------|---------|
| `IBillingAccountAccessor` | Scoped | The host's accessor |
| `IGrantActionGate` | Scoped | Shows every grant action, unless the host registered its own gate |
| Call gate | Scoped | Queues the components' service calls so one circuit never runs two at once |
| `TimeProvider` | Singleton | `TimeProvider.System`, unless the host registered one |

## 4) Gate Grant Actions

Each create, edit and delete control asks `IGrantActionGate` whether to render. The default shows them all; the server still refuses what the host's decorators refuse. To hide controls the caller may not use, register a gate before `AddBillingWeb`:

```csharp
public interface IGrantActionGate
{
    RenderFragment Gate(GrantAction action, Guid? grantId, RenderFragment authorized, RenderFragment? notAuthorized = null);
}
```

| Control | Action | Grant id |
|---------|--------|----------|
| New grant, and the empty list's create link | `Create` | none |
| A row's Edit link; View when not authorized | `Update` | the row's |
| A row's Delete button | `Delete` | the row's |
| The editor for a new grant | `Create` | none |
| The editor for an existing grant; read-only when not authorized | `Update` | the grant's |

## 5) Include Styles

Component styles are scoped and arrive through the host's own bundle, which a Blazor template already links:

```html
<link rel="stylesheet" href="MyApp.styles.css" />
```

Bootstrap 5.3 and Bootstrap Icons come from the host.

## Notes

- Components render in the host's render mode. They need interactivity: use `InteractiveServer`.
- Authorization belongs in the host's service decorators, through `BillingOptions.DecorateServices`. See the [Authorization](../../Corely.Billing/Docs/authorization.md) docs.
- The components share the circuit's DI scope, so scoped state such as a signed-in user reaches the host's decorators.
- For the routed pages, see [Routed Pages](pages.md).
