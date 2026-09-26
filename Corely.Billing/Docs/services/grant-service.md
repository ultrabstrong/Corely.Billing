# IGrantService

Manages grants: an account's allowance of one unit for one operation, valid between two instants.

## Methods

| Method | Parameters | Returns |
|--------|-----------|---------|
| `CreateGrantAsync` | `CreateGrantRequest` | `CreateGrantResult` |
| `GetGrantAsync` | `Guid accountId, Guid grantId` | `RetrieveSingleResult<Grant>` |
| `ListGrantsAsync` | `ListGrantsRequest`, `IReadOnlySet<Guid>? authorizedResourceIds` | `RetrieveListResult<Grant>` |
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
    Filter: Filter.For<Grant>().Where(g => g.Quantity, ComparableFilter<long>.GreaterThanOrEqual(100)),
    Order: Order.For<Grant>().By(g => g.ValidToUtc, SortDirection.Ascending),
    Skip: 0, Take: 25));

var grants = result.Data?.Items;
```

Without an order, grants list newest `ValidFromUtc` first.

`authorizedResourceIds` narrows the list to those grant ids, for an authorization decorator that knows which grants the caller may read; `null` leaves it unrestricted. It narrows the page and the total together, and leaves the request's own filter untouched. Corely.IAM's list processors take the same parameter.

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
