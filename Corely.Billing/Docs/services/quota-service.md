# IQuotaService

Holds quota before work, settles it against what the work cost, and gives it back if the work failed. Every call except `GetAvailabilityAsync` runs inside an [operation scope](../operation-context.md).

## Methods

| Method | Parameters | Returns |
|--------|-----------|---------|
| `GetAvailabilityAsync` | `Guid accountId, UsageOperation, UsageUnit` | `QuotaAvailability` |
| `ReserveAsync` | `ReserveQuotaRequest` | `ReserveQuotaResult` |
| `SettleAsync` | `SettleQuotaRequest` | `SettleQuotaResult` |
| `ReleaseAsync` | `ReleaseQuotaRequest` | `SettleQuotaResult` |

## Usage

### Front-Door Check

```csharp
var availability = await quotaService.GetAvailabilityAsync(accountId, op, unit);
if (availability == QuotaAvailability.Exhausted)
    return Refuse();
```

`Unknown` means the check could not run. Let the work start; reservation is the accurate check. A gate that must fail closed, such as a subscription, treats `Unknown` as a refusal instead.

### Reserve, Settle, Release

```csharp
using var scope = accessor.BeginScope(new OperationContext(correlationId, scopeName));

var reserved = await quotaService.ReserveAsync(
    new ReserveQuotaRequest(accountId, op, unit, Quantity: 1, Provider: "mistral", UserId: userId));

try
{
    var actual = await DoWorkAsync();
    var settled = await quotaService.SettleAsync(new SettleQuotaRequest(accountId, op, unit, actual));
}
catch (WorkFailedException)
{
    await quotaService.ReleaseAsync(new ReleaseQuotaRequest(accountId, op, unit));
}
```

## Results

| Property | On | Meaning |
|----------|----|---------|
| `Shares` | `ReserveQuotaResult` | The `GrantShare` per grant the hold drew on |
| `SettledQuantity` | `SettleQuotaResult` | What was charged |
| `Overdrawn` | `SettleQuotaResult` | The charge exceeded every live grant; the last one absorbed it |
| `RemainingRatio` | `SettleQuotaResult` | What is left across live grants, 0 to 1; 1 while an unlimited grant is live |

## Notes

- `ReserveQuotaRequest.Provider` is who performs the work, for reporting by provider
- `ReserveQuotaRequest.Tags` are copied onto every row the reservation writes
- `NoGrantAvailableError` means no live grant at all; `InsufficientQuotaError` means grants exist without enough room across them
- See the [Reservations](../reservations.md) docs for retry and overdraft behavior
