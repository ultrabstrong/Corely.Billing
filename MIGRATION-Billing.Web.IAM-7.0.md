# Migrating to Corely.Billing.Web.IAM 7.0

Billing's API now answers in the one shape `Corely.IAM.Web` 9.0 defines. There is no database
migration.

```json
{ "resultCode": "Success", "message": "", "data": { "remaining": 1200 } }
```

```json
{ "resultCode": "NoCapacityError", "message": "No capacity is left", "data": null }
```

| Package | Version |
|---------|---------|
| Corely.Billing.Web.IAM | 7.0.0 |
| Corely.IAM.Web | 9.0.0 |

## 1. Read results from `data`

A result's fields move from the top level to `data`, and `resultCode` and `message` stay at the top. A
list's page is `data` itself. The status codes have not moved.

## 2. Read failures from the top level

A failure's `data` is null, and a 400 for a malformed operation or unit is the same shape, not a
validation problem. Its `message` names the field.

## 3. Turn on the shape for every failure

So that an empty 401, 403 or 404 gets the shape too, add this before `UseIAMWebAuthentication`. See
[Migrating to Corely.IAM.Web 9.0](https://github.com/ultrabstrong/Corely.IAM/blob/master/MIGRATION-IAM.Web-9.0.md).

```csharp
app.UseIAMApiResponses("/api");
```
