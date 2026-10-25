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
| `DeclarationMismatchError` | The kind or unit disagrees with what the operation declares |
| `UnauthorizedError` | Refused by a host decorator |

### DeleteGrantResultCode

| Code | Meaning |
|------|---------|
| `Success` | Grant deleted |
| `NotFoundError` | Not found for this account |
| `UnauthorizedError` | Refused by a host decorator |

## Grant Request Result Codes

### RequestGrantResultCode

| Code | Meaning |
|------|---------|
| `Success` | Request recorded as pending |
| `ValidationError` | Input validation failed |
| `AlreadyPendingError` | The account already has a pending request for this operation |
| `UnauthorizedError` | Refused by a host decorator |

### DecideGrantRequestResultCode

Returned by both `ApproveGrantRequestAsync` and `DeclineGrantRequestAsync`.

| Code | Meaning |
|------|---------|
| `Success` | Decided; an approval's `GrantId` names the new grant |
| `NotFoundError` | Not found for this account |
| `AlreadyDecidedError` | Approved or declined already; nothing changed |
| `ValidationError` | A decline's reason is too long, or an approval's window fails grant validation; nothing changed |
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

### QuotaReadResultCode

Returned by `GetBalanceAsync`, `GetAllowanceAsync`, `HasFeatureAsync` and `GetCapacityAsync`.

| Code | Meaning |
|------|---------|
| `Success` | Read; the amounts are set |
| `WrongKindError` | The operation is not registered, or is declared as another kind |
| `UnauthorizedError` | Refused by a host decorator |

## Registration Result Codes

### RegisterResultCode

| Code | Meaning |
|------|---------|
| `Registered` | A seat is held; `Registration` names it and its grant |
| `AlreadyRegisteredError` | The reference already holds a seat for this operation |
| `NoCapacityError` | Active grants exist, and every seat on them is taken |
| `NoActiveGrantError` | No active capacity grant for this operation |
| `WrongKindError` | The operation is not registered, or is not a capacity |
| `ValidationError` | Input validation failed |
| `UnauthorizedError` | Refused by a host decorator |

### MoveRegistrationsResultCode

| Code | Meaning |
|------|---------|
| `Success` | Moved; `Moved` lists the new registrations |
| `NotFoundError` | Either grant is not found for this account |
| `WrongKindError` | The grants are not capacities of the same operation |
| `NoCapacityError` | The target lacks room for every registration; nothing moved |
| `UnauthorizedError` | Refused by a host decorator |
