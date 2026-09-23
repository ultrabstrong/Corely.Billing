# IGrantService

Manages grants: an account's allowance of one unit for one operation, valid between two instants.

## Methods

| Method | Parameters | Returns |
|--------|-----------|---------|
| `CreateGrantAsync` | `CreateGrantRequest` | `CreateGrantResult` |
| `GetGrantAsync` | `Guid accountId, Guid grantId` | `RetrieveSingleResult<Grant>` |
| `ListGrantsAsync` | `ListGrantsRequest` | `RetrieveListResult<Grant>` |
| `UpdateGrantAsync` | `UpdateGrantRequest` | `ModifyResult` |
| `DeleteGrantAsync` | `Guid accountId, Guid grantId` | `DeleteGrantResult` |

## Usage

### Create

```csharp
var result = await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, MyUsage.DocumentExtraction, MyUsage.Page, Quantity: 500,
    ValidFromUtc: periodStart, ValidToUtc: periodStart.AddMonths(1),
    Tags: new() { ["plan"] = "growth" }));

var grantId = result.CreatedId;
```

### Unlimited

A null `Quantity` is an unlimited grant. Quota draws on it before any limited grant and never finds it short.

```csharp
await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, MyUsage.DocumentExtraction, MyUsage.Page, Quantity: null, termStart, termStart.AddYears(1)));
```

### List with Filtering and Pagination

```csharp
var result = await grantService.ListGrantsAsync(new ListGrantsRequest(
    accountId,
    Filter: Filter.For<Grant>().Where(g => g.ValidToUtc, ComparableFilter<DateTime>.GreaterThan(now)),
    Order: Order.For<Grant>().By(g => g.ValidToUtc, SortDirection.Ascending),
    Skip: 0, Take: 25));

var grants = result.Data?.Items;
```

Without an order, grants list newest `ValidFromUtc` first.

### Update

```csharp
await grantService.UpdateGrantAsync(new UpdateGrantRequest(
    accountId, grantId, MyUsage.Page, Quantity: 750, periodStart, periodEnd));
```

## Validation

| Rule | Code |
|------|------|
| `AccountId` and `GrantId` not empty | `ValidationError` |
| `Quantity` zero or more, or null for unlimited | `ValidationError` |
| `ValidToUtc` after `ValidFromUtc` | `ValidationError` |
| Operation and unit registered | `ValidationError` |

## Notes

- Every method is scoped by `accountId`; another account's grant reads as `NotFoundError`
- An update cannot change a grant's operation; create a new grant instead
- Deleting a grant leaves its consumption rows in place
- A grant is live while `ValidFromUtc <= now <= ValidToUtc`
