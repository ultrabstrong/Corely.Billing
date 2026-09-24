# Setup

Adds the components to a Blazor Server host that already registers Corely.Billing.

## 1) Install the Package

```bash
dotnet add package Corely.Billing.Web
```

## 2) Tell the Components Which Account

Implement `IBillingAccountAccessor`. `CanManageGrantsAsync` defaults to `true`; override it to hide the create, edit and delete controls.

```csharp
internal sealed class MyAccountAccessor(IBlazorUserContextAccessor users, IAuthorizationProvider authorization)
    : IBillingAccountAccessor
{
    public async Task<Guid?> GetAccountIdAsync() =>
        (await users.GetUserContextAsync())?.CurrentAccount?.Id;

    public Task<bool> CanManageGrantsAsync() =>
        authorization.IsAuthorizedAsync(AuthAction.Update, "grants");
}
```

The example is a Corely.IAM host. A host with one fixed account returns that id.

## 3) Register Services

```csharp
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddBillingServices(billingOptions);
builder.Services.AddBillingWeb<MyAccountAccessor>();
```

| Registered | Lifetime | Purpose |
|------------|----------|---------|
| `IBillingAccountAccessor` | Scoped | The host's accessor |
| Call gate | Scoped | Queues the components' service calls so one circuit never runs two at once |
| `TimeProvider` | Singleton | `TimeProvider.System`, unless the host registered one |

## 4) Include Styles

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
