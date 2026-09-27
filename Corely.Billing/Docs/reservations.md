# Reservations

Quota is held before work starts and settled to what the work actually cost. The hold is a row in the ledger, so a second caller sees it and cannot spend the same quota.

## Features

- **Hold before work**: `ReserveAsync` refuses work the account cannot pay for
- **Settle to the truth**: `SettleAsync` corrects the hold to the real quantity
- **Release on failure**: `ReleaseAsync` gives the hold back, keeping the row for audit
- **TTL expiry**: a hold nobody resolves stops counting after `ReservationTtl`
- **Grant-edge splitting**: one charge is spread across grants, soonest-expiring first
- **Overdraft-once**: settlement never refuses work already done

## Lifecycle

1. `ReserveAsync` splits the requested quantity across live grants and writes one outstanding row per grant.
2. The host does the work.
3. `SettleAsync` re-splits the actual quantity across the grants live now, settles each held row, and writes a settled row for any grant it newly draws on.
4. Or `ReleaseAsync` marks every held row released, leaving its quantity as it was.

When the real size is unknown, reserve an estimate and let settlement correct it. The most the work can
cost, such as a model request's token limit, refuses work the account could not pay for at worst.
`Quantity: 1` refuses only an account with nothing left.

```csharp
await quotaService.ReserveAsync(new ReserveQuotaRequest(accountId, op, unit, maxTokens, "large-model"));
var reply = await GenerateAsync(prompt, maxTokens);
await quotaService.SettleAsync(new SettleQuotaRequest(accountId, op, unit, reply.TotalTokens));
```

## What Counts Against a Grant

| Row state | Counts |
|-----------|--------|
| Settled | Yes |
| Outstanding, within `ReservationTtl` | Yes |
| Outstanding, past `ReservationTtl` | No |
| Released | No |

## Grant Selection

Live grants are those whose window contains the current time. Unlimited grants (a null `Quantity`) are spent first, and take the whole charge. Limited grants follow in order of `ValidToUtc`, then smallest `Quantity`, then `ValidFromUtc`, then `GrantId`, so quota the account would otherwise lose is used first. The order is total, so a replay allocates exactly as the original did.

## Overdraft

When the actual quantity exceeds every live grant's remainder, settlement still records it. The overrun lands on the last grant it drew on, which reads past its quantity. `SettleQuotaResult.Overdrawn` reports it, and the grant then reports no room rather than negative room.

## Retries

| Situation | Behavior |
|-----------|----------|
| Reserve replayed while the hold is outstanding | No-op, `Success` |
| Reserve replayed after settlement | No-op, `Success`; the ledger is unchanged |
| Reserve retried after a release | Holds again on the same row |
| Settle replayed with nothing outstanding | No-op, `Success` |
| Two attempts inserting the same key at once | The loser reports `Success` for the winner's row |

## Notes

- `SettleQuotaResult.RemainingRatio` is what is left across live grants after the charge, from 0 to 1, and 1 while an unlimited grant is live
- `GetAvailabilityAsync` answers without a quantity and returns `Unknown` rather than failing when the database is unreachable
- `IConsumptionService.CountAbandonedReservationsAsync` counts holds that expired unresolved. A rising number means work is dying between reserve and settle
