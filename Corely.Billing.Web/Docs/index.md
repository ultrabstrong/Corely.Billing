# Corely.Billing.Web Documentation

Blazor Server components for Corely.Billing: a grant list and editor for every grant kind, a registration list for capacity, a usage chart with six views, a usage event table, and CSV export of all of it. A host places them in its own pages, at its own paths, and passes every link.

- **No identity library**: the host says which account, and a gate decides which grant actions to show; [Corely.Billing.Web.IAM](../../Corely.Billing.Web.IAM/Docs/index.md) wires both to [Corely.IAM](https://github.com/ultrabstrong/Corely.IAM)
- **Authorization stays in the host**: an `UnauthorizedError` from a decorated service shows as a message
- **Display names, not tokens**: every label comes from the registered usage vocabulary
- **Every grant kind its own way**: a meter for a balance, how much is registered for a capacity, the amount added for an allowance, enabled or disabled for a feature; a null quantity reads "Unlimited", and a balance used past its quantity reads overdrawn
- **Charts that break usage down**: stacked, side by side, remaining balance, burn-up and share, one unit at a time
- **Raw data out**: CSV of events, grants and chart series, or one zip with a README describing every column
- **Chart.js vendored**: the chart loads its own copy; the host adds nothing to its layout
- **Light and dark**: styled from Bootstrap's tokens, so the host's `data-bs-theme` applies

## Topics

- [Setup](setup.md)
- [Components](components/index.md)
    - [GrantList](components/grant-list.md)
    - [GrantEditor](components/grant-editor.md)
    - [GrantRequestForm](components/grant-request-form.md)
    - [RegistrationList](components/registration-list.md)
    - [UsageChart](components/usage-chart.md)
    - [ConsumptionTable](components/consumption-table.md)
    - [UsageDashboard](components/usage-dashboard.md)
- [Export](export.md)
- [Hosting the Components](hosting-components.md)
- [Styling](styling.md)

## Quick Start

```csharp
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddBillingServices(billingOptions);
builder.Services.AddBillingWeb<MyAccountAccessor>();
```

```razor
<GrantList AccountId="accountId" NewGrantHref="/grants/new" RequestGrantHref="/grants/request" GrantHref="id => $&quot;/grants/{id}&quot;" />
<UsageDashboard AccountId="accountId" />
```
