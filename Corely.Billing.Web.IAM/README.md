# Corely.Billing.Web.IAM

[Corely.IAM.Web](https://github.com/ultrabstrong/Corely.IAM/blob/master/Corely.IAM.Web/Docs/index.md) for [Corely.Billing.Web](https://github.com/ultrabstrong/Corely.Billing). The Billing components take the account from the signed-in user, and each grant action (New, Edit, Delete) is gated by IAM's `PermissionView` against the caller's permissions on `grant`.

```csharp
builder.Services.AddIAMServices(iamOptions.RegisterBillingResourceTypes());
builder.Services.AddBillingServices(billingOptions.UseCorelyIamPermissions());
builder.Services.AddBillingWebIam();
```

`app.MapBillingApiEndpoints()` serves quota reads, registrations, grants and consumption to a caller
holding an IAM API key. `app.MapBillingMeteringEndpoints()` adds reserve, settle and release, for a
host that meters over HTTP.

See the [documentation](https://github.com/ultrabstrong/Corely.Billing/blob/master/Corely.Billing.Web.IAM/Docs/index.md).
