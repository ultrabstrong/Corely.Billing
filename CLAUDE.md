# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Host-agnostic grants, consumption and quota for .NET applications: what an account was given, what it used, and whether any is left, reconciled across retries. Extracted from DocsToData, which remains its first consumer. Held to Corely.IAM's conventions — where IAM has a way of doing something, this repository does it the same way. Targets .NET 10.0.

## Build Commands

```powershell
# Full rebuild, format, and test
.\RebuildAndTest.ps1

# Build
dotnet build Corely.Billing.slnx
```

## Testing

### Tiers

Each tier owns exactly one seam.

| Tier | Owns | Substrate | Project |
|------|------|-----------|---------|
| Unit | One class's logic, dependencies substituted | No database | `Corely.Billing.UnitTests`, `Corely.Billing.Web.UnitTests` (bUnit), `Corely.Billing.DataAccessMigrations.Cli.UnitTests` |
| Integration | Persistence — EF translation, schema, provider behavior, and the composed grant/ledger balance | SQLite / Testcontainers | `Corely.Billing.IntegrationTests` |
| Functional | HTTP — the demo hosts start, route, serve assets, and a Razor Pages flow runs | `WebApplicationFactory` in-process | `Corely.Billing.Web.FunctionalTests` |

### Where does a new test go?

Walk down and stop at the **first** tier that can prove the case:

1. Provable with no database? → **Unit**
2. Needs real SQL translation, real schema, provider behavior, or a balance read back through the ledger? → **Integration**
3. Needs the HTTP pipeline — a host starting, routing, static assets, antiforgery? → **Functional**. A Blazor circuit is out of this tier's reach; component behavior is bUnit's.

**A case proven at tier N is never re-proven at tier N+1.** If a case seems to fit two tiers, it is usually two cases. Split it.

**A business-visible number produced by two subsystems belongs to the tier that can observe it.** Every layer once had passing tests while a grant with one unit left was charged five hundred. `GrantConsumptionLifecycleTests` states the invariant in the terms an invoice would: what each grant's balance reads after the work.

**Prove a regression test catches the regression.** Reintroduce the bug, watch the test go red, then restore.

**`MockRepo` stays for the unit tier.** Put behavior that depends on the database in the integration tier rather than trying to make the mock more faithful. Do not propose replacing the repository layer with the EF in-memory provider, raw `DbContext` injection or `DbSet` mocking; the reasoning is in `Corely.DataAccess/DESIGN-RATIONALE.md`.

### Running tests

The test projects run on **xunit.v3 / Microsoft.Testing.Platform**. `global.json` at the repo root opts `dotnet test` into MTP mode and is read from the working directory, so run from the repository root. Projects and solutions are named with `--project` / `--solution`, and `--filter` becomes `--filter-query`.

```powershell
# Everything
dotnet test --solution Corely.Billing.slnx

# Unit tier
dotnet test --project Corely.Billing.UnitTests
dotnet test --project Corely.Billing.Web.UnitTests
dotnet test --project Corely.Billing.DataAccessMigrations.Cli.UnitTests

# Integration tier — real EF on SQLite. No external dependencies.
dotnet test --project Corely.Billing.IntegrationTests

# Functional tier — the demo hosts in-process on SQLite. No external dependencies.
dotnet test --project Corely.Billing.Web.FunctionalTests

# Single class / method. The filter is /assembly/namespace/class/method.
dotnet test --project Corely.Billing.UnitTests --filter-query "/*/*/QuotaProcessorTests/*"
```

A filter matching nothing exits **8**, not 0. Check the reported test count.

### Provider matrix (opt-in, needs Docker)

Both shipped providers are exercised by Testcontainers against the real migrations. Skipped by default:

```powershell
$env:CORELY_RUN_CONTAINER_TESTS = "1"
dotnet test --project Corely.Billing.IntegrationTests --filter-query "/*/*/*ProviderMatrixTests/*"
```

Run it after any migration or any change to a query. If Docker is not running, start it; Docker being down is never a reason to report a behavior as unverifiable.

**When a tier is added, its run command goes here in the same change.**

## Code Formatting

CSharpier enforced via MSBuild integration. Files are auto-formatted on build.

**IMPORTANT for Claude Code:** After making changes, ALWAYS run `.\RebuildAndTest.ps1` to format, rebuild, and test everything before committing.

## Migrations

```powershell
# Run from repo root — all scripts target both DB providers (MySQL, SQL Server)
.\AddMigration.ps1 "MigrationName"    # Creates migration in all providers
.\RemoveMigration.ps1                  # Removes last migration from all providers
.\ListMigrations.ps1                   # Lists migrations (no DB connection needed)
```

Billing records migrations in `__CorelyBillingMigrationsHistory`, so it shares a database with Corely.IAM and a host's own contexts.

## Architecture

### Solution Structure

| Project | Purpose |
|---------|---------|
| `Corely.Billing` | Core library — grants, the consumption ledger, quota (net10.0) |
| `Corely.Billing.Web` | Blazor Server components and opt-in routed pages. Versioned on its own. No reference to Corely.IAM |
| `Corely.Billing.Demos.Portal` / `.Subscription` / `.WithIAM` | Demo hosts on LocalDB, schema from the migration CLI. Smoke-tested in `Corely.Billing.Web.FunctionalTests/Demos`, which references them through extern aliases because every host's top-level `Program` is public |
| `Corely.Billing.Demos.Bootstrap` | Bootstrap for the demos, served as a static web asset so it is vendored once |
| `Corely.Billing.ConsoleTest` | Zero-setup demo on SQLite |
| `Corely.Billing.UnitTests` | Unit tests (xUnit, Moq) on mock repositories |
| `Corely.Billing.IntegrationTests` | SQLite tests and the opt-in provider matrix |
| `Corely.Billing.DataAccessMigrations.Cli` | Migration CLI, published as the `corely-billing-db` .NET tool. Its **major** version tracks `Corely.Billing`'s |
| `Corely.Billing.DataAccessMigrations.MySql` | MySQL EF Core migrations |
| `Corely.Billing.DataAccessMigrations.MsSql` | SQL Server EF Core migrations |

### Layered Architecture

```
Services (public) → Processors (internal) → Repositories/UoW → BillingDbContext → Database
```

Processors are wrapped with **telemetry decorators** via Scrutor, which record the `BillingMetricNames` metrics. Services are wrapped with telemetry decorators that log. Between the two, the host's own decorators run — `BillingOptions.DecorateServices` — which is where authorization goes.

Registration order in `ServiceRegistrationExtensions.cs` matters: decorators are applied bottom-up (last registered = outermost).

### Domain Structure

Each domain (Grants, Consumption, Quota) follows IAM's folder layout:

```
Domain/
├── Constants/        # Domain constants (SCREAMING_SNAKE_CASE)
├── Entities/         # EF Core entities and configurations
├── Models/           # Request/response/domain models
├── Processors/       # Business logic + telemetry decorators
├── Mappers/          # Entity ↔ Model mapping
└── Validators/       # FluentValidation rules
```

### Boundaries that are the point of the library

- **No vocabulary.** Operations and units are tokens a host registers on `BillingOptions`. Nothing in this repository may name a consumer's operation or unit outside a test.
- **No identity library.** Authorization is a host concern, applied through `DecorateServices`. Referencing Corely.IAM from here would force every consumer of billing to take identity with it. `Corely.Billing.Web` holds to this too; only `Corely.Billing.Demos.WithIAM` references IAM.
- **Web components share the circuit's scope.** They queue their calls through one scoped gate instead of owning scopes, because a host's authorization decorators read scoped user state that a fresh scope would not have.
- **No telemetry backend.** Metrics go through `IBillingTelemetry` with unprefixed names; the host prefixes and exports them.
- **Ledger writes go through quota.** `IConsumptionService` only reads. Every consumption row is tied to a reservation against a grant.

### Data Layer

- One internal `BillingDbContext` for `Grants` and `ConsumptionEvents`, configured via `IEFConfiguration`
- Entity configurations auto-discovered via reflection in `BillingDbContext.OnModelCreating`
- Operations and units stored as their tokens; tags as JSON; ids are version 7 GUIDs generated by the library

### DI Registration

- **Production**: `BillingOptions.Create(configuration, efConfigFactory)` — EF Core repositories and UoW
- **Testing**: `BillingOptions.Create(configuration)` — in-memory mock repositories

## Development Patterns

### Philosophy

Favor brevity over verbosity when planning and writing code. Code that isn't written cannot break, and doesn't need to be maintained.

### Time Abstraction

Use `TimeProvider`, never `DateTime.UtcNow`. `AddBillingServices` registers `TimeProvider.System` only if the host has not registered one. No test sleeps on the wall clock — drive time with `FakeTimeProvider`.

### Result Pattern

An outcome the caller is expected to handle is a result code, not an exception — out of quota, an unregistered unit, no operation scope. Exceptions are for faults. Error codes end in `Error`; enums carry no explicit ordinals.

### Naming Conventions

- `Service` = public top-level API; `Processor` = internal business logic; `Repo` = repository (internal)
- `Model` = domain data objects; `Entity` = database data objects
- `_camelCase` for private fields, `PascalCase` for properties/methods, `SCREAMING_SNAKE_CASE` for constants
- `Async` suffix on all async methods; `CancellationToken ct` last and propagated
- One class/enum/interface per file; no `#region` tags
- Test methods: `<MethodName>_<ExpectedBehavior>_For<InputDescription>`

### Seams for readings and conversions

**A reading or conversion of a type gets a seam of its own, not a private helper.** A conversion
between two types, or a reading of one type that anything else could want, is a contract someone
can get wrong, and as a private helper it can only be reached through the class that calls it. Give
it a home a unit test can call directly:

- **A type you own, in the same layer: a member of that type.** A record or model gets the method
  itself — `quotaContext.RemainingRatio(charged)`, `retryOptions.RetryDelay(attempt, random)`. No
  extension class for a type you can simply change.
- **A type you cannot change: an extension.** Enums, BCL types and vendor types —
  `TimeBucket.BucketStart`, `TimeSpan.AgoText`, a broker's message.
- **A type you own whose reading belongs to another layer: an extension in that layer.** Display text
  for a core model lives in the UI project; a conversion to another domain's result lives with that
  domain, so the source type never learns about it.
- **Entities stay plain data.** Their conversions go in the domain's `Mappers`.

An extension is `<Type>Extensions`, in an `Extensions` or `Mappers` folder, holding extensions of
that one type, written as a C# 14 `extension(T receiver)` block:

```csharp
internal static class TimeBucketExtensions
{
    extension(TimeBucket bucket)
    {
        public string PeriodLabel(DateTime bucketStart) => ...;
    }
}
```

Either way, name it for what comes back (`RemainingRatio`, `ToSettleQuotaResult`,
`PlaceholderConnectionString`), keep it `internal` with `InternalsVisibleTo` in the `.csproj`, and
test it directly.

**Don't**:
- leave it as a private helper on the class that happens to need it, including the instance form
  that closes over a field instead of taking a parameter;
- inline the conversion where it is used, with no seam at all;
- write an extension for a type you own and could give the member to;
- write a helper class not attached to a type — `MessageHelper`, `MappingUtils`, a grab-bag
  `Extensions.cs` or `...Messages` class holding readings of several unrelated types;
- name it for the receiver or the mechanics — `RemainingRatioOf`, `GetRatioFromContext`;
- use `this T` parameters in new code (existing ones convert when next touched), or make anything
  `public` purely so a test can reach it. Public is for real API only.

This covers conversions and derived readings, not every private method: a helper that only
structures the code it sits in stays where it is, and so does a private step inside an extension
class.

### Plans

Store implementation plans in `Plans/` at the repository root.

### Rejected Ideas

[`DESIGN-DECISIONS.md`](DESIGN-DECISIONS.md) records ideas that were dropped but are easy to remember as done. Check it before assuming such a change exists. Add an entry only when the user asks for one.

## Documentation

`Docs/` describes **how the current version works**. Nothing else.

- **No version numbers of this library.** Migration guides are the sole exception and live at the repository root.
- **No references to `Plans/`.**
- **Match the house style.** Terse and code-forward. Read the neighbouring files in `Docs/` before adding one.

The full guide is [`DOCUMENTATION-STYLE.md`](DOCUMENTATION-STYLE.md) at this repository root.
