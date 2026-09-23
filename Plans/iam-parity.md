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

## Progress

Done, committed locally:
- Library reshaped to the target above; unit tests on mock repos; SQLite integration tests including
  the grant/ledger balance invariants moved from DocsToData.
- `Corely.Billing.DataAccessMigrations.MsSql` / `.MySql` with baseline `InitialMigration`s, the
  `corely-billing-db` tool and its unit tests, the migration scripts, and an opt-in provider matrix
  that passes on SQL Server and MySQL containers.
- Docs, README, CLAUDE.md, DOCUMENTATION-STYLE.md, DESIGN-DECISIONS.md, Feature-Ideas, settings,
  CI/release packing both packages, and the zero-setup `Corely.Billing.ConsoleTest` demo.
- DocsToData consumes the new surface (its own repository).

Open for the owner:
- Create `ultrabstrong/Corely.Billing` on GitHub and push `master`.
- nuget.org trusted-publisher policy bound to `release.yml`, package scope `Corely.Billing*`.
- Tag `v1.0.0-preview.1`.
- Known rough edges, each a decision rather than a bug:
  - IAM and Billing both define `Models.RetrieveResultCode` (and `ModifyResult`, `PagedResult`);
    a file importing both namespaces needs an alias. The fix is to move them into Corely.Common.
  - IAM and Billing each register `IEFConfiguration`; in one container the last one wins for both
    DbContexts. Harmless while both share a database.