# Migrating to Corely.Billing.Web 7.0 and Corely.Billing.Web.IAM 9.0

Every action of a Billing component now runs in a DI scope of its own. In 6.x a component owned one
scope for as long as it was on screen, so a list reloaded after a change made elsewhere (a grant
request cancelled, say) kept showing the rows its `DbContext` first loaded.

## What breaks

| Before | After |
|--------|-------|
| `BillingComponentBase` and `BillingPageBase` inherit `OwningComponentBase` and expose `ScopedServices` | Both inherit `BillingOperationComponentBase` and expose `InOperationAsync(services => ...)` |
| `BillingComponentBase.OnParametersSetSignedInAsync()` | Override `OnParametersSetAsync()`; each operation signs its own scope in |
| Corely.Billing.Web.IAM on Corely.IAM.Web 9 | On Corely.IAM.Web 11, whose `IOperationScopes` it registers as `IBillingOperationScopes` |

## What to do

1. A component of your own that derived from a Billing base class replaces its `ScopedServices`
   properties with `InOperationAsync`, resolving services from the provider it is handed.
2. A host's own component that calls Billing services inherits `BillingOperationComponentBase`, or
   injects `IBillingOperationScopes` and calls `RunAsync` the same way.
3. A host on Corely.Billing.Web.IAM moves to Corely.IAM.Web 11 with it; its
   [migration guide](https://github.com/ultrabstrong/Corely.IAM/blob/master/MIGRATION-IAM.Web-11.0.md)
   covers the IAM side.

[Setup](Corely.Billing.Web/Docs/setup.md) states the rules.
