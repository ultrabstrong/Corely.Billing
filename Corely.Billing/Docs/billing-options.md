# BillingOptions Configuration

`BillingOptions` is the single builder for all Corely.Billing configuration. It holds the database configuration, the usage vocabulary, telemetry, and the host's service decorators.

## Features

- **Static factory** — `Create()` enforces required parameters via method signature
- **Fluent chaining** — optional configuration methods return `this`
- **Registered vocabulary** — operations and units are declared once, at startup
- **Pluggable telemetry** — a no-op default; supply your own metrics sink
- **Host decorators** — wrap the public services with authorization or anything else
- **Two paths** — EF production path and mock testing path from the same API

## Usage

### Production Setup (Entity Framework)

```csharp
var options = BillingOptions.Create(configuration, efConfigFactory)
    .RegisterOperation("document_extraction", "Document Extraction")
    .RegisterUnit("page", "page");
services.AddBillingServices(options);
```

The configuration `efConfigFactory` builds is private to Billing: it is registered under a key only
Billing's DbContext resolves, never as a plain `IEFConfiguration`. A host's own DbContexts register
their own.

### Test Setup (Mock Repositories)

```csharp
var options = BillingOptions.Create(configuration)
    .RegisterOperation("document_extraction", "Document Extraction")
    .RegisterUnit("page", "page");
services.AddBillingServices(options);
```

The `Create()` overload without `efConfigurationFactory` registers in-memory mock repositories instead of EF Core. Their data lives as long as the DI scope that resolved them.

### With Telemetry and Decorators

```csharp
var options = BillingOptions.Create(configuration, efConfigFactory)
    .RegisterOperation("document_extraction", "Document Extraction")
    .RegisterUnit("page", "page")
    .UseTelemetry(sp => sp.GetRequiredService<MyBillingTelemetry>())
    .DecorateServices(s => s.Decorate<IQuotaService, QuotaAuthorizationDecorator>());
```

## Create() Overloads

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `configuration` | `IConfiguration` | Yes | App configuration for options binding (`ReservationOptions`) |
| `efConfigurationFactory` | `Func<IServiceProvider, IEFConfiguration>` | No | Database provider factory; omit for the mock path |

## Fluent Methods

| Method | Parameters | Description |
|--------|-----------|-------------|
| `RegisterOperation` | `value`, `displayName` | Adds an operation to the vocabulary |
| `RegisterUnit` | `value`, `displayName` | Adds a unit to the vocabulary |
| `UseTelemetry` | `Func<IServiceProvider, IBillingTelemetry>` | Replaces the no-op telemetry |
| `DecorateServices` | `Action<IServiceCollection>` | Wraps the public services; may be called more than once |

A duplicate operation or unit overwrites the earlier display name. See the [Usage Vocabulary](usage-vocabulary.md) docs for token rules.

## What AddBillingServices Registers

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `IGrantService` | Scoped | Create, read, update, delete and list grants |
| `IQuotaService` | Scoped | Availability, reserve, settle, release |
| `IConsumptionService` | Scoped | Ledger reporting |
| `IUsageVocabulary` | Singleton | The registered operations and units |
| `IOperationContextAccessor` | Singleton | The ambient unit of work (kept if already registered) |
| `IBillingTelemetry` | Singleton | Metrics sink |
| `TimeProvider` | Singleton | `TimeProvider.System`, kept if already registered |

`AddBillingServices` throws `InvalidOperationException` when no operation or no unit is registered.

## Configuration Sections

| Section | Options Class | Properties |
|---------|--------------|------------|
| `ReservationOptions` | `ReservationOptions` | `ReservationTtl` (`06:00:00`) |

```json
{
  "ReservationOptions": {
    "ReservationTtl": "06:00:00"
  }
}
```

Production note: keep the TTL generous. Release is the primary way a hold ends; the TTL only catches a process killed mid-flight.
