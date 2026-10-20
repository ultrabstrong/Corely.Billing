# Migrating to Corely.Billing.Web 3.0 and Corely.Billing.Web.IAM 3.0

Corely.Billing.Web now ships components only. Its routed pages and `BillingWebRoutes` are gone, and
every link a component renders is the host's. Corely.Billing, Corely.Billing.IAM and the migration
CLI do not change. Corely.Billing.Web.IAM moves to Corely.IAM.Web 7.0; see
[its guide](https://github.com/ultrabstrong/Corely.IAM/blob/master/MIGRATION-IAM.Web-7.0.md).

## 1. Stop routing the library

Remove this assembly from the router and from endpoint mapping:

```razor
<Router AppAssembly="typeof(Routes).Assembly">
```

```csharp
app.MapRazorComponents<App>().AddInteractiveServerRenderMode(); // no AddAdditionalAssemblies
```

## 2. Write the shells

`GrantsPage`, `GrantEditorPage`, `GrantRequestPage` and `UsagePage` moved to the demos. Copy them
from `Corely.Billing.Demos.Portal/Components/Pages` (simple paths) or
`Corely.Billing.Demos.WithIAM/Components/Pages` (account paths with Corely.IAM.Web's
`AccountScope`) and give them your paths. `Corely.Billing.Web/Docs/hosting-components.md` shows a
shell.

## 3. Pass every link

`GrantList`'s `NewGrantHref`, `RequestGrantHref` and `GrantHref` are required, with no default:

```razor
<GrantList AccountId="accountId"
           NewGrantHref="/grants/new"
           RequestGrantHref="/grants/request"
           GrantHref="id => $&quot;/grants/{id}&quot;" />
```

## 4. `BillingPageBase`

| Was | Now |
|-----|-----|
| `Corely.Billing.Web.Pages.BillingPageBase` | `Corely.Billing.Web.Components.BillingPageBase` |
| `AccountId` | `CurrentAccountId`, so a shell can take an `AccountId` route parameter beside it |
