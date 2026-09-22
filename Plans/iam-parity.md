# IAM parity

Corely.Billing is held to Corely.IAM's standard: the same conventions, tooling, docs and ease of use.
Where IAM has a way of doing something, Billing does it the same way. This plan lists every place
the extracted code differs and what replaces it.

## Public surface

| Today | Becomes |
|---|---|
| `AddUsageVocabulary` + `AddEntitlementServices` + `AddMeteringServices` + `AddQuotaServices` | `BillingOptions.Create(configuration[, efConfigurationFactory])` then `AddBillingServices(options)` |
| `UsageVocabularyBuilder.Operation/Unit` | `BillingOptions.RegisterOperation/RegisterUnit`, like `IAMOptions.RegisterResourceType` |
| Host must register `IBillingTelemetry` | `BillingOptions.UseTelemetry(factory)`; a no-op default |
| `decorate` hook per `Add…Services` | `BillingOptions.DecorateServices(action)` |
| Public `IGrantReader`, `IGrantWriter`, `IConsumptionReader`, `IConsumptionWriter` | Internal `IGrantProcessor`, `IConsumptionProcessor` behind public `IGrantService`, `IConsumptionService` |
| `IQuotaService` holding the quota logic | `IQuotaService` (public, thin) over internal `IQuotaProcessor` |
| `MeteringOptions`, section `Metering` | `ReservationOptions`, section `ReservationOptions` |
| Immutable models with `Create()` returning `CreateResult<T>` | Mutable models, request records, FluentValidation validators through `IValidationProvider` |
| Result codes `Unauthorized`, `NotFound`, explicit ordinals | `UnauthorizedError`, `NotFoundError`, … no ordinals |
| Ad hoc single/list results | `RetrieveSingleResult<T>`, `RetrieveListResult<T>`, `PagedResult<T>`, `ModifyResult` |

Services are named for their domain rather than IAM's verbs. A host using both libraries would
otherwise import two `IRegistrationService`s.

Authorization stays out of the library. `DecorateServices` is where a host adds it, around the three
public services only; the internal processors are never host-visible.

## Library layout

Per domain, IAM's folders: `Constants/`, `Entities/` (entity + `…EntityConfiguration`), `Mappers/`
(extension methods), `Models/`, `Processors/` (processor + telemetry decorator), `Validators/`.
Shared: `DataAccess/` (`BillingDbContext`, `MigrationConstants`), `Extensions/`, `Models/`,
`Services/`, `Validators/`. Constructor arguments are guarded with Corely.Common's `ThrowIfNull`.

## Schema

- One internal `BillingDbContext` for grants and consumption.
- `Corely.Billing.DataAccessMigrations.MsSql` and `.MySql`, unpublished, each with a baseline
  `InitialMigration` from the current model.
- `Corely.Billing.DataAccessMigrations.Cli`, published as the `corely-billing-db` tool, with IAM's
  commands, options and environment variables (`CORELY_BILLING_DB_PROVIDER`,
  `CORELY_BILLING_DB_CONNECTION`) and its own unit tests.
- History in `__CorelyBillingMigrationsHistory`, so billing shares a database with IAM and the host.
- `AddMigration.ps1`, `RemoveMigration.ps1`, `ListMigrations.ps1` at the root.

## Tests

| Tier | Project | Substrate |
|---|---|---|
| Unit | `Corely.Billing.UnitTests`, `Corely.Billing.DataAccessMigrations.Cli.UnitTests` | `MockRepo` through `BillingOptions` without EF |
| Integration | `Corely.Billing.IntegrationTests` | SQLite; Testcontainers provider matrix opt-in via `CORELY_RUN_CONTAINER_TESTS=1` |

## Repository

- Root: `CLAUDE.md`, `README.md`, `DOCUMENTATION-STYLE.md`, `DESIGN-DECISIONS.md`, `CONTRIBUTING.md`,
  `LICENSE`, `Plans/` with `Feature-Ideas.md`, `.claude/settings.json`, `RebuildAndTest.ps1`
  (publishes the CLI), `scripts/check-package-versions.sh` covering both packages.
- CI and release as IAM's; release packs the library and the CLI.
- `Corely.Billing/Docs/` and `Corely.Billing.DataAccessMigrations.Cli/Docs/` per
  `DOCUMENTATION-STYLE.md`.
- `Corely.Billing.ConsoleTest`: runs grant → reserve → settle on SQLite with no setup.

Not carried over: `CopyCorelyTools.ps1` (IAM-specific and stale), DevTools, WebApp.

## DocsToData afterwards

Consumes the new surface: `BillingOptions`, the three services, authorization via `DecorateServices`,
telemetry via `UseTelemetry`. Its billing migrations retire; schema comes from `corely-billing-db`.
Existing databases adopt the baseline without losing rows.

## Progress (uncommitted in Corely.Billing working tree)

Done:
- Library reshaped to the target above; `Corely.Billing` builds clean. `BillingOptions`,
  `AddBillingServices`, three public services over internal processors (Grant, Consumption,
  ConsumptionReport, Quota), single internal `BillingDbContext`, FluentValidation validators,
  shared `Models/` results, `ListQueryHelper`, `NullBillingTelemetry`, `ReservationOptions`.
- `Corely.Billing.IntegrationTests` created (SQLite `BillingTestHost`); 39 passing: reporting,
  reservation ledger, entity configuration, grant listing via FilterBuilder/OrderBuilder.
- Unit tests rewritten for grants: validator, mapper, processor, telemetry decorator; new
  `ServiceFactory` (mock repos) and `TestUsage`.

Next, in order:
1. Unit tests still on the old API, to port then delete: Consumption/Services/ConsumptionWriterTests
   (-> Consumption/Processors/ConsumptionProcessorTests, Mock<IRepo> cases; SaveAsync cases become
   ReserveAsync), Consumption/Models/ConsumptionEventTests (-> Validators/ConsumptionEventValidatorTests;
   Stamp tests drop), Consumption/Mappers/ConsumptionEventMapperTests, IdempotencyKeyFactoryTests
   (namespace only), both consumption telemetry decorator tests, Quota/Services/QuotaServiceTests
   (-> QuotaProcessorTests over mocked processors), QuotaTelemetryDecoratorTests,
   Usage/UsageVocabularyTests (builder gone; BillingOptions), plus new BillingOptionsTests,
   ServiceRegistrationExtensionsTests, ReserveQuotaRequestValidatorTests, service decorator tests.
   Then commit.
2. Migration projects (MsSql, MySql) with baseline InitialMigration, CLI + CLI unit tests copied
   from IAM, AddMigration/RemoveMigration/ListMigrations scripts, provider matrix tests.
3. Repo files: CLAUDE.md, README, DOCUMENTATION-STYLE.md, DESIGN-DECISIONS.md, Plans/Feature-Ideas.md,
   .claude/settings.json, RebuildAndTest.ps1, check-package-versions (library + CLI), release packs CLI,
   Docs/ per project, ConsoleTest demo.
4. DocsToData consumes the new surface; its billing migrations retire; existing databases adopt the
   baseline (decision for the user: adopt in place vs. recreate).
