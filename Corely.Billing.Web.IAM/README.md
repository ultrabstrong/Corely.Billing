# Corely.Billing.Web.IAM

[Corely.IAM.Web](https://github.com/ultrabstrong/Corely.IAM) for [Corely.Billing.Web](https://github.com/ultrabstrong/Corely.Billing). The Billing components take the account from the signed-in user, and each grant action — New, Edit, Delete — is gated by IAM's `PermissionView` against the caller's permissions on `grant`.

```csharp
builder.Services.AddIAMServices(iamOptions.RegisterBillingResourceTypes());
builder.Services.AddBillingServices(billingOptions.UseCorelyIamPermissions());
builder.Services.AddBillingWebIam();
```

See the [documentation](https://github.com/ultrabstrong/Corely.Billing/blob/master/Corely.Billing.Web.IAM/Docs/index.md).
