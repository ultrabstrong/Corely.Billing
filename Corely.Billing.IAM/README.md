# Corely.Billing.IAM

[Corely.IAM](https://github.com/ultrabstrong/Corely.IAM) permissions for [Corely.Billing](https://github.com/ultrabstrong/Corely.Billing). Registers the `grant`, `consumption` and `quota` resource types with IAM, and decorates the Billing services so every call checks the caller's account and CRUDX permissions.

```csharp
services.AddIAMServices(iamOptions.RegisterBillingResourceTypes());
services.AddBillingServices(billingOptions.UseCorelyIamPermissions());
```

See the [documentation](https://github.com/ultrabstrong/Corely.Billing/blob/master/Corely.Billing.IAM/Docs/index.md).
