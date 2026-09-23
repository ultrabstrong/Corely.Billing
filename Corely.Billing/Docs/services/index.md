# Services

Three public services form the API surface of Corely.Billing. All are registered as scoped and wrapped with telemetry decorators.

## Service Overview

| Service | Purpose | Methods |
|---------|---------|---------|
| `IGrantService` | Create, read, update, delete and list grants | 5 |
| `IQuotaService` | Availability, reserve, settle, release | 4 |
| `IConsumptionService` | Totals, time series, event listing, ledger health | 7 |

Writes to the ledger go through `IQuotaService` only. That is what keeps every consumption row tied to a reservation against a grant.

## Decorator Pattern

```
TelemetryDecorator → [Host decorators] → Service → Processor decorators → Processor
```

- **Host decorators** — whatever the host adds through `BillingOptions.DecorateServices`, typically authorization
- **Telemetry decorators** — log entry, exit and timing; they sit outside the host's, so a denied call is logged too
- **Processor decorators** — record the metrics listed in [Telemetry](../telemetry.md)

## Service vs Processor

| | Service | Processor |
|--|---------|-----------|
| **Visibility** | `public` | `internal` |
| **Purpose** | The host's API | Business logic, data access |
| **Decoratable by the host** | Yes | No |

## Result Pattern

Expected outcomes are result codes, not exceptions:

```csharp
var result = await quotaService.ReserveAsync(request);
if (result.ResultCode == ReserveQuotaResultCode.InsufficientQuotaError)
    return Refuse("Out of quota for this cycle");
```

See the [Result Codes](../result-codes.md) docs for every code.

## Topics

- [Grant Service](grant-service.md)
- [Quota Service](quota-service.md)
- [Consumption Service](consumption-service.md)
