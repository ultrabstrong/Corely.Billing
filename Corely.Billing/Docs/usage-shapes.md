# Usage Shapes

Corely.Billing does not require reservations, overlapping grants, or a metered pipeline. An app uses as much of it as it needs. The schema is the same for every shape, and a table with no rows costs nothing.

| Shape | Grants | Consumption | Question the app asks |
|-------|--------|-------------|-----------------------|
| Metered | Quantities that expire and overlap | Reserved before work, settled to the real cost | Is there room for this much work, and what did it cost? |
| Subscription | One unlimited grant per term | None, or one row per visit | Does this account have a live grant right now? |
| Prepaid credits | One pack per purchase, far-future expiry | Settled per use | How much is left, and when do I prompt a top-up? |
| Trial | A small grant with a short window | Settled per use | Has the trial run out, by time or by quantity? |
| Seats or feature access | One grant per feature, quantity is seats | None | Does the account have this feature, and how many seats? |

Runnable examples are `Corely.Billing.Demos.Portal` (metered), `Corely.Billing.Demos.Subscription`, and `Corely.Billing.Demos.WithIAM` (metered, signed in through Corely.IAM) in the repository.

## Metered

Every part of the library. See the [Reservations](reservations.md) docs.

```csharp
using var scope = accessor.BeginScope(new OperationContext(correlationId, $"job:{jobId}/step:extract"));
await quotaService.ReserveAsync(new ReserveQuotaRequest(accountId, extraction, page, 1, "mistral"));
var pages = await ExtractAsync(document);
await quotaService.SettleAsync(new SettleQuotaRequest(accountId, extraction, page, pages));
```

## Subscription

A term is a grant with a null `Quantity`, which is unlimited. Cancelling moves `ValidToUtc` to now.

```csharp
await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, membersArea, visit, Quantity: null, now, now.AddYears(1)));
```

Gate on availability. `Unknown` means the check could not run; a subscription gate fails closed, so anything but `Available` is a refusal.

```csharp
var open = await quotaService.GetAvailabilityAsync(accountId, membersArea, visit)
    == QuotaAvailability.Available;
```

To count activity, record each visit as a reserve and settle of one under its own operation scope. The rows are tied to the term's grant, so `GetGrantConsumptionTotalsAsync` answers "visited N times this term".

```csharp
using var scope = accessor.BeginScope(new OperationContext(Guid.CreateVersion7(), $"visit:{visitId}"));
await quotaService.ReserveAsync(new ReserveQuotaRequest(accountId, membersArea, visit, 1, "web"));
await quotaService.SettleAsync(new SettleQuotaRequest(accountId, membersArea, visit, 1));
```

## Prepaid Credits

Each purchase is a grant with a far-future expiry. Packs are spent soonest-expiring first, so an older pack runs out before a newer one.

```csharp
await grantService.CreateGrantAsync(new CreateGrantRequest(
    accountId, generation, credit, Quantity: 1_000, now, now.AddYears(10)));
```

`SettleQuotaResult.RemainingRatio` is what is left across live packs after the charge. Prompt a top-up when it drops below a threshold.

```csharp
var settled = await quotaService.SettleAsync(new SettleQuotaRequest(accountId, generation, credit, used));
if (settled.RemainingRatio < 0.1)
    await PromptTopUpAsync(accountId);
```

## Trial

A small grant with a short window. `ReserveAsync` says which limit was hit.

```csharp
var reserved = await quotaService.ReserveAsync(new ReserveQuotaRequest(accountId, op, unit, 1, "app"));
var reason = reserved.ResultCode switch
{
    ReserveQuotaResultCode.NoGrantAvailableError => "The trial has ended",
    ReserveQuotaResultCode.InsufficientQuotaError => "The trial allowance is used up",
    _ => null,
};
```

## Seats or Feature Access

One grant per feature, with `Quantity` as the seat count. Nothing is consumed. The account has the feature while a grant is live; the seats are the live grants' quantities.

```csharp
var live = await grantService.ListGrantsAsync(new ListGrantsRequest(accountId,
    Filter: Filter.For<Grant>()
        .Where(g => g.ValidFromUtc, ComparableFilter<DateTime>.LessThanOrEqual(now))
        .Where(g => g.ValidToUtc, ComparableFilter<DateTime>.GreaterThanOrEqual(now))));
var grants = live.Data!.Items.Where(g => g.Operation == reporting).ToList();
var seats = grants.Any(g => g.Quantity is null) ? null : grants.Sum(g => g.Quantity);
```

## Notes

- Operations and units are tokens the filter builder cannot compare, so filter by them after the query, as above.
- An unlimited grant is spent before any limited grant it overlaps, and never runs short.
- The Blazor components in [Corely.Billing.Web](../../Corely.Billing.Web/Docs/index.md) print a null quantity as "Unlimited" and draw no capacity for it.
