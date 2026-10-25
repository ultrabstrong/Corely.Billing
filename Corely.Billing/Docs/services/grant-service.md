# IGrantService

Manages grants: what an account holds of one operation, of one [kind](../usage-shapes.md), valid between two instants.

## Methods

| Method | Parameters | Returns |
|--------|-----------|---------|
| `CreateGrantAsync` | `CreateGrantRequest` | `CreateGrantResult` |
| `GetGrantAsync` | `Guid accountId, Guid grantId` | `RetrieveSingleResult<Grant>` |
| `ListGrantsAsync` | `ListGrantsRequest`, `IReadOnlySet<Guid>? authorizedResourceIds` | `RetrieveListResult<Grant>` |
| `UpdateGrantAsync` | `UpdateGrantRequest` | `ModifyResult` |
| `SetGrantDisabledAsync` | `SetGrantDisabledRequest` | `ModifyResult` |
| `DeleteGrantAsync` | `Guid accountId, Guid grantId` | `DeleteGrantResult` |

## Usage

### Create

```csharp
var result = await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, MyUsage.TextGeneration, GrantKind.Consumable, Quantity: 1_000_000,
    ValidFromUtc: periodStart, ValidToUtc: periodStart.AddMonths(1),
    Tags: new() { ["plan"] = "growth" }));

var grantId = result.CreatedId;
```

The unit is the operation's declared unit when `Unit` is left null. A kind or unit that disagrees with the declaration is `DeclarationMismatchError`.

### Unlimited

A null `Quantity` is unlimited: an unlimited balance, an unlimited capacity or an unlimited allowance. Quota draws on an unlimited consumable before any limited grant and never finds it short.

```csharp
await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, MyUsage.TextGeneration, GrantKind.Consumable, Quantity: null, termStart, termStart.AddYears(1)));
```

### A Feature

A feature has no quantity and no unit.

```csharp
await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, MyUsage.SftpAccess, GrantKind.Feature, Quantity: null, now, now.AddYears(1)));
```

### Never Expires

A grant that ends on a lapse rather than a date, such as a subscription, takes `GrantConstants.NEVER_EXPIRES_UTC` as its `ValidToUtc`. `Grant.NeverExpires()` reads it back.

### List with Filtering and Pagination

```csharp
var result = await grantService.ListGrantsAsync(new ListGrantsRequest(
    [accountId],
    Kind: GrantKind.Allowance,
    Operation: MyUsage.Retention,
    ActiveAtUtc: now,
    Order: Order.For<Grant>().By(g => g.ValidToUtc, SortDirection.Ascending),
    Skip: 0, Take: 25));

var grants = result.Data?.Items;
```

`Kind`, `Operation` and `ActiveAtUtc` narrow the list; `Filter` takes any other condition. Without an order, grants list newest `ValidFromUtc` first.

More than one id in `AccountIds` is a cross-account read, for a platform role. The library itself reads every account it is given; refusing it is for a host decorator, as [Corely.Billing.IAM](../../../Corely.Billing.IAM/Docs/index.md) does.

`authorizedResourceIds` narrows the list to those grant ids, for an authorization decorator that knows which grants the caller may read; `null` leaves it unrestricted. It narrows the page and the total together, and leaves the request's own filter untouched. [Corely.IAM](https://github.com/ultrabstrong/Corely.IAM)'s list processors take the same parameter.

### Each Grant's State

Every `Grant` that `GetGrantAsync` and `ListGrantsAsync` return carries its state, so a list needs no second call.

| Property | Set for | Meaning |
|----------|---------|---------|
| `Used` | Consumable | Settled and held against it |
| `Remaining` | Consumable | What is left, never below zero; null for unlimited |
| `InUse` | Capacity | Active registrations on it |

`GetGrantType()` reads the display type from the kind and whether a quantity is set: `Balance`, `Unlimited`, `Capacity`, `UnlimitedCapacity`, `Allowance`, `UnlimitedAllowance` or `Feature`.

### Update

```csharp
await grantService.UpdateGrantAsync(new UpdateGrantRequest(
    accountId, grantId, Quantity: 1_500_000, periodStart, periodEnd));
```

### Disable and Enable

```csharp
await grantService.SetGrantDisabledAsync(new SetGrantDisabledRequest(accountId, grantId, Disabled: true));
```

A disabled grant gives out nothing new: quota does not reserve against it, register on it or count it in any read, as if it had expired. Work that already holds quota on it finishes there; its settlement still charges the grant it held. Its registrations stay on it and report `GrantDisabled`. Enable it again with `Disabled: false`. `UpdateGrantAsync` never changes the flag.

## Validation

| Rule | Code |
|------|------|
| `AccountId` and `GrantId` not empty | `ValidationError` |
| `Quantity` zero or more, or null for unlimited | `ValidationError` |
| `ValidToUtc` after `ValidFromUtc` | `ValidationError` |
| Operation registered, and unit registered when given | `ValidationError` |
| A unit for every kind but a feature; no quantity and no unit for a feature | `ValidationError` |
| Kind and unit match the operation's declaration | `DeclarationMismatchError` |

## Notes

- Every method is scoped by `accountId`; another account's grant reads as `NotFoundError`
- An update cannot change a grant's operation, kind or unit; create a new grant instead
- A grant whose operation is no longer registered still reads, because its kind is on the row
- Deleting a grant leaves its consumption rows in place, and releases a capacity grant's registrations
- A grant is active while `ValidFromUtc <= now <= ValidToUtc` and it is not disabled
