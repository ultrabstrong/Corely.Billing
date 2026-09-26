# Corely.Billing.Web Documentation

Blazor Server components for Corely.Billing: a grant list and editor, a usage chart, and a usage event table. A host drops them into its own pages, or opts in to four routed pages that use them.

- **No identity library** — the host says which account, and a gate decides which grant actions to show; [Corely.Billing.Web.IAM](../../Corely.Billing.Web.IAM/Docs/index.md) wires both to Corely.IAM
- **Authorization stays in the host** — an `UnauthorizedError` from a decorated service shows as a message
- **Display names, not tokens** — every label comes from the registered usage vocabulary
- **Unlimited and overdrawn grants** — a null quantity reads "Unlimited"; a grant used past its quantity reads overdrawn
- **Chart.js vendored** — the chart loads its own copy; the host adds nothing to its layout
- **Light and dark** — styled from Bootstrap's tokens, so the host's `data-bs-theme` applies

## Topics

- [Setup](setup.md)
- [Components](components/index.md)
    - [GrantList](components/grant-list.md)
    - [GrantEditor](components/grant-editor.md)
    - [UsageChart](components/usage-chart.md)
    - [ConsumptionTable](components/consumption-table.md)
    - [UsageDashboard](components/usage-dashboard.md)
- [Routed Pages](pages.md)
- [Styling](styling.md)

## Quick Start

```csharp
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddBillingServices(billingOptions);
builder.Services.AddBillingWeb<MyAccountAccessor>();
```

```razor
<GrantList AccountId="accountId" />
<UsageDashboard AccountId="accountId" />
```
