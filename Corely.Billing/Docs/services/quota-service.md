# IQuotaService

Answers what an account holds for each [grant kind](../usage-shapes.md), and makes the writes each kind allows: reserve and settle for a consumable, register and release for a capacity. `ReserveAsync`, `SettleAsync` and `ReleaseAsync` run inside an [operation scope](../operation-context.md); nothing else needs one.

## Methods

| Method | Kind | Returns |
|--------|------|---------|
| `GetBalanceAsync(accountId, operation)` | Consumable | `GetBalanceResult` |
| `ReserveAsync(ReserveQuotaRequest)` | Consumable | `ReserveQuotaResult` |
| `SettleAsync(SettleQuotaRequest)` | Consumable | `SettleQuotaResult` |
| `ReleaseAsync(ReleaseQuotaRequest)` | Consumable | `SettleQuotaResult` |
| `GetAllowanceAsync(accountId, operation)` | Allowance | `GetAllowanceResult` |
| `HasFeatureAsync(accountId, operation)` | Feature | `HasFeatureResult` |
| `GetCapacityAsync(accountId, operation)` | Capacity | `GetCapacityResult` |
| `RegisterAsync(RegisterRequest)` | Capacity | `RegisterResult` |
| `ReleaseRegistrationAsync(accountId, registrationId)` | Capacity | `ModifyResult` |
| `MoveRegistrationsAsync(MoveRegistrationsRequest)` | Capacity | `MoveRegistrationsResult` |
| `ListRegistrationsAsync(ListRegistrationsRequest)` | Capacity | `RetrieveListResult<Registration>` |

## Usage

### Reads

Each read adds up the account's active grants of the operation: not disabled, started, not ended. Every amount is null when one of those grants is unlimited.

| Result | Amounts |
|--------|---------|
| `GetBalanceResult` | `Remaining`, and `HasAny` for anything left at all |
| `GetAllowanceResult` | `Total` |
| `HasFeatureResult` | `HasFeature` |
| `GetCapacityResult` | `Granted`, `InUse`, and `Free` |

Each also carries the `Grants` it read. A read for an operation declared as another kind returns `WrongKindError` and nothing else.

```csharp
var balance = await quotaService.GetBalanceAsync(accountId, extraction);
if (!balance.HasAny)
    return Refuse();
```

### Reserve, Settle, Release

```csharp
using var scope = accessor.BeginScope(new OperationContext(correlationId, scopeName));

var reserved = await quotaService.ReserveAsync(
    new ReserveQuotaRequest(accountId, op, unit, Quantity: maxTokens, Provider: "large-model", UserId: userId));

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

### Register and Release

```csharp
var registered = await quotaService.RegisterAsync(
    new RegisterRequest(accountId, teamMembers, Reference: $"user:{userId}", Description: userName));

await quotaService.ReleaseRegistrationAsync(accountId, registered.Registration!.RegistrationId);
```

`MoveRegistrationsAsync` releases each active registration on `FromGrantId` and registers it again on `ToGrantId`, all of them or the `RegistrationIds` given. It moves nothing when the target lacks room for every one.

## Results

| Property | On | Meaning |
|----------|----|---------|
| `Shares` | `ReserveQuotaResult` | The `GrantShare` per grant the hold drew on |
| `SettledQuantity` | `SettleQuotaResult` | What was charged |
| `Overdrawn` | `SettleQuotaResult` | The charge exceeded every live grant; the last one absorbed it |
| `RemainingRatio` | `SettleQuotaResult` | What is left across live grants, 0 to 1; 1 while an unlimited grant is live |
| `Registration` | `RegisterResult` | What was registered, with the grant it went on |
| `Status` | `Registration` | `Active`, `Released`, `GrantEnded` or `GrantDisabled`, read from its grant at query time |

## Notes

- `ReserveQuotaRequest.Provider` is who performs the work, for reporting by provider
- `ReserveQuotaRequest.Tags` are copied onto every row the reservation writes
- `NoGrantAvailableError` means no live grant at all; `InsufficientQuotaError` means grants exist without enough room across them
- A registration goes on the active grant with room that ends last; registering is atomic, so two requests for the last place cannot both succeed
- A `Reference` already registered on an active grant of the operation is `AlreadyRegisteredError`; leave it null for registrations that need no identity
- A registration on an ended or disabled grant holds nothing: the reference can register again, and goes on an active grant like any other
- Deleting a capacity grant releases its registrations
- A database the read cannot reach throws, as any fault does; it is not reported as an empty balance
- See the [Reservations](../reservations.md) docs for retry and overdraft behavior
