# Corely Billing
Grants, consumption and quota: what an account was given, what it used, and whether any is left —
kept reconciled across retries, so a replayed unit of work is never charged twice.

## Installation
`dotnet add package Corely.Billing`

## Getting Started
The library ships no vocabulary. An application registers the operations it charges for and the
units it charges in, once, at startup:

```csharp
services.AddUsageVocabulary(vocabulary =>
    vocabulary
        .Operation("document_extraction", "Document Extraction")
        .Unit("page", "page")
);

services.AddSingleton<IBillingTelemetry, MyBillingTelemetry>();

services.AddEntitlementServices();
services.AddMeteringServices();
services.AddQuotaServices();
```

Anything outside that vocabulary is refused with an `UnknownUsage` result rather than stored.

- **Grants** (`Corely.Billing.Grants`) — `IGrantReader`, `IGrantWriter`: quantities of a unit an
  account may consume, and when.
- **Consumption** (`Corely.Billing.Consumption`) — `IConsumptionReader`, `IConsumptionWriter`: what
  was used, as reservations that are later settled or released.
- **Quota** (`Corely.Billing.Quota`) — `IQuotaService`: reserve before the work, settle against what
  it actually cost, release if it failed.

Every write happens inside an operation scope, which is what makes a retry recognise its own earlier
rows:

```csharp
using var scope = operationContextAccessor.BeginScope(
    new OperationContext(correlationId, IdempotencyScope: $"job:{jobId}/step:{stepId}")
);
```

### What the host provides
- An `IEFConfiguration` from Corely.DataAccess. The two DbContexts, `EntitlementsDbContext` and
  `MeteringDbContext`, are public so the host can compose them and own their migration history.
- An `IBillingTelemetry`. There is deliberately no default: metric names are relative
  (`quota.settlement.quantity`), and the host decides what to prefix them with.
- Authorization, if any. Each `Add…Services` takes an optional `decorate` hook that runs between the
  service and its telemetry decorator, so the library never references an identity library.
- `Metering:ReservationTtl` in configuration, if six hours is not right.

## Repository
[Corely.Billing](https://github.com/ultrabstrong/Corely.Billing)

## Contributing
We welcome contributions! Please read our [contributing guidelines](https://github.com/ultrabstrong/Corely.Billing/blob/master/CONTRIBUTING.md) to get started.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
