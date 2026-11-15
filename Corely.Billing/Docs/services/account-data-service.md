# IAccountDataService

Everything billing keeps for one account, removed when the host deletes that account.

## Methods

| Member | Parameters | Returns |
|--------|-----------|---------|
| `DeleteAccountDataAsync` | `Guid accountId` | `DeleteAccountDataResult` |

```csharp
var result = await accountData.DeleteAccountDataAsync(accountId, ct);
// result.ConsumptionEvents, Registrations, GrantRequests, Grants: rows deleted from each table
```

## What it deletes

Every row of the account, leaves first so nothing is deleted while a row still names it:

1. Consumption events, open reservations included, so no hold needs releasing first
2. Registrations
3. Grant requests
4. Grants

Prices are platform wide and are not touched. Running it again deletes nothing and succeeds, so a
host can retry a deletion that stopped halfway.

## Who may call it

The core library applies no authorization. With `UseCorelyIamPermissions()`, only the system
context deletes (`UnauthorizedError` for anyone else, even the account's owner): a host calls it
from the code that deletes the account, not from a page.

## Keeping it complete

A new entity that stores data for an account is deleted here. `AccountDataDeletionTests` lists every
entity in the model and fails on one that is neither deleted nor exempt with a reason.
