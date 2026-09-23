# Result Codes

Complete reference of all result code enums across Corely.Billing. `UnauthorizedError` is never returned by the library itself; it exists for host [authorization](authorization.md) decorators.

## Common Result Codes

### RetrieveResultCode

| Code | Meaning |
|------|---------|
| `Success` | Found |
| `NotFoundError` | Not found for this account |
| `UnauthorizedError` | Refused by a host decorator |

### ModifyResultCode

| Code | Meaning |
|------|---------|
| `Success` | Update applied |
| `NotFoundError` | Not found for this account |
| `UnauthorizedError` | Refused by a host decorator |
| `ValidationError` | Input validation failed |

## Grant Result Codes

### CreateGrantResultCode

| Code | Meaning |
|------|---------|
| `Success` | Grant created |
| `ValidationError` | Input validation failed |
| `UnauthorizedError` | Refused by a host decorator |

### DeleteGrantResultCode

| Code | Meaning |
|------|---------|
| `Success` | Grant deleted |
| `NotFoundError` | Not found for this account |
| `UnauthorizedError` | Refused by a host decorator |

## Quota Result Codes

### ReserveQuotaResultCode

| Code | Meaning |
|------|---------|
| `Success` | Held; `Shares` lists the grants drawn on |
| `ValidationError` | Input validation failed |
| `UnauthorizedError` | Refused by a host decorator |
| `NoGrantAvailableError` | No live grant for this operation and unit |
| `InsufficientQuotaError` | Live grants exist without enough room across them |
| `NotRecordedError` | The hold could not be written, or no operation scope is open; do not start the work |

### SettleQuotaResultCode

Returned by both `SettleAsync` and `ReleaseAsync`.

| Code | Meaning |
|------|---------|
| `Success` | Resolved, or nothing was outstanding |
| `UnauthorizedError` | Refused by a host decorator |
| `NotRecordedError` | The ledger could not be updated, or no operation scope is open; the hold stays until its TTL |

### QuotaAvailability

| Value | Meaning |
|-------|---------|
| `Unknown` | Could not be determined; let the work start |
| `Available` | Some room across live grants |
| `Exhausted` | No live grant with anything left |
