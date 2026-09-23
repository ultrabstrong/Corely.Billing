# Operation Context

`OperationContext` identifies the unit of work a quota call belongs to. It is ambient: the host opens it once at the edge of the work, and billing reads it far below through `IOperationContextAccessor`.

## Features

- **Ambient** — nothing between the edge and billing carries it as a parameter
- **Idempotency scope** — names the billable work, and is the same on every retry of it
- **Correlation** — joins every ledger row to the logs of the work that caused it
- **Async-flowing** — backed by `AsyncLocal`, so it follows `await` and does not leak between requests

## Usage

```csharp
using var scope = accessor.BeginScope(
    new OperationContext(correlationId, $"job:{jobId}/step:{stepName}"));

await quotaService.ReserveAsync(request);
```

## The Idempotency Scope

Every row's idempotency key is built from the scope, the operation, the unit and the grant:

```
{IdempotencyScope}|{operation}|{unit}|{grantId:N}
```

A unique index on `(AccountId, IdempotencyKey)` is what turns a retry into a no-op instead of a second charge.

| Scope | Result |
|-------|--------|
| Same value on every retry of one unit of work | Charged once |
| New value per attempt (a fresh `Guid`) | Charged on every retry |
| Same value for two different units of work | The second is never charged |

Build the scope from identifiers the work already has — a job id and step name, a message id — never from the time or a random value.

## Notes

- Without an open scope, reserve, settle and release return `NotRecordedError` and write nothing
- The scope is at most 400 characters once the operation, unit and grant are appended
- `AddBillingServices` keeps an `IOperationContextAccessor` the host registered first
