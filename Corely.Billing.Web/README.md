# Corely.Billing.Web

Blazor Server components for [Corely.Billing](https://github.com/ultrabstrong/Corely.Billing): a grant list and editor, a usage chart, and a usage event table, plus four opt-in routed pages.

```csharp
builder.Services.AddBillingServices(billingOptions);
builder.Services.AddBillingWeb<MyAccountAccessor>();
```

```razor
<GrantList AccountId="accountId" />
<UsageDashboard AccountId="accountId" />
```

See the [documentation](https://github.com/ultrabstrong/Corely.Billing/blob/master/Corely.Billing.Web/Docs/index.md).
