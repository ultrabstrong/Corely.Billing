# BillingOptions Configuration

`BillingOptions` is the single builder for all Corely.Billing configuration. It holds the database configuration, the usage vocabulary, telemetry, and the host's service decorators.

## Features

- **Static factory**: `Create()` enforces required parameters via method signature
- **Fluent chaining**: optional configuration methods return `this`
- **Registered vocabulary**: operations, their grant kind and their unit are declared once, at startup
- **Pluggable telemetry**: a no-op default; supply your own metrics sink
- **Host decorators**: wrap the public services with authorization or anything else
- **Two paths**: EF production path and mock testing path from the same API

## Usage

### Production Setup (Entity Framework)

```csharp
var options = BillingOptions.Create(configuration, efConfigFactory)
    .RegisterUnit("token", "token")
    .RegisterOperation("text_generation", "Text Generation", GrantKind.Consumable, "token");
services.AddBillingServices(options);
```

The configuration `efConfigFactory` builds is private to Billing: it is registered under a key only
Billing's DbContext resolves, never as a plain `IEFConfiguration`. A host's own DbContexts register
their own.

### Test Setup (Mock Repositories)

```csharp
var options = BillingOptions.Create(configuration)
    .RegisterUnit("token", "token")
    .RegisterOperation("text_generation", "Text Generation", GrantKind.Consumable, "token");
services.AddBillingServices(options);
```

The `Create()` overload without `efConfigurationFactory` registers in-memory mock repositories instead of EF Core. Their data lives as long as the DI scope that resolved them.

### With Telemetry and Decorators

```csharp
var options = BillingOptions.Create(configuration, efConfigFactory)
    .RegisterUnit("token", "token")
    .RegisterOperation("text_generation", "Text Generation", GrantKind.Consumable, "token")
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
| `RegisterOperation` | `value`, `displayName`, `kind`, `unit` | Adds an operation, declaring its grant kind and its unit; a feature takes no unit |
| `RegisterUnit` | `value`, `displayName`, `decimals` (default 0, at most 6) | Adds a unit to the vocabulary, counted to that many decimal places |
| `UseTelemetry` | `Func<IServiceProvider, IBillingTelemetry>` | Replaces the no-op telemetry |
| `DecorateServices` | `Action<IServiceCollection>` | Wraps the public services; may be called more than once |
| `UseCompiledModel` | `IModel` | An EF Core model built ahead of time; normally set through a compiled model package, below |

A duplicate operation or unit overwrites the earlier display name. See the [Usage Vocabulary](usage-vocabulary.md) docs for token rules.

## Compiled Model

Entity Framework builds Billing's model the first time an app uses it, which on a small host can
take tens of seconds and lands on whichever request comes first. A compiled model package holds that
model built ahead of time for one database provider:

| Package | Provider | Call |
|---------|----------|------|
| `Corely.Billing.CompiledModel.MsSql` | SQL Server | `.UseMsSqlCompiledModel()` |
| `Corely.Billing.CompiledModel.MySql` | MySQL and MariaDB | `.UseMySqlCompiledModel()` |

```csharp
var options = BillingOptions.Create(configuration, efConfigFactory)
    .UseMsSqlCompiledModel();
```

Reference the package for the provider the app's `IEFConfiguration` uses; a model for another
provider describes the wrong column types. Without a package nothing changes: the model is built at
first use, as before.

A compiled model package is released with `Corely.Billing` at the same version, and its call refuses
to start against any other `Corely.Billing` version. Inside this repository, `AddMigration.ps1` and
`RemoveMigration.ps1` regenerate both models through `UpdateCompiledModels.ps1`, and
`CompiledModelTests` fails when either no longer describes the same tables, columns, keys and
indexes as the model built at runtime.

## What AddBillingServices Registers

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `IGrantService` | Scoped | Create, read, update, delete and list grants |
| `IQuotaService` | Scoped | A read per grant kind, reserve and settle, register and release |
| `IConsumptionService` | Scoped | Ledger reporting |
| `IUsageVocabulary` | Singleton | The registered operations and units |
| `IOperationContextAccessor` | Singleton | The ambient unit of work (kept if already registered) |
| `IBillingTelemetry` | Singleton | Metrics sink |
| `TimeProvider` | Singleton | `TimeProvider.System`, kept if already registered |

`AddBillingServices` throws `InvalidOperationException` when no operation is registered, when an operation names a unit that is not registered, when a feature names a unit, or when any other kind names none.

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
