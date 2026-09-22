# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project Overview

Grants, consumption and quota for any application that sells a quantity of something and needs the
two sides to reconcile. Extracted from DocsToData, which remains its first consumer.

## Boundaries that are the point of the library

- **No vocabulary.** Operations and units are strings an application registers at startup through
  `AddUsageVocabulary`. Nothing in this repository may name a consumer's operation or unit outside
  a test.
- **No identity library.** Authorization is a host concern, applied through the `decorate` hook on
  each `Add…Services`. Referencing Corely.IAM from here would force every consumer of billing to take
  identity with it.
- **No migrations.** The library ships entity types, configurations and two public DbContexts; the
  host owns the migration history. A table this library alters is a table two packages would race to
  alter.
- **No telemetry backend.** Metrics go through `IBillingTelemetry` with relative names; the host
  prefixes and exports them.

## Build and test

```powershell
.\RebuildAndTest.ps1
```

Tests run on xunit.v3 / Microsoft.Testing.Platform. `global.json` opts `dotnet test` into MTP mode,
and is only read from the working directory, so run from the repository root. Projects and solutions
are named with `--project` / `--solution` rather than positionally.

```powershell
dotnet test --solution Corely.Billing.slnx
```

Persistence tests run on in-memory SQLite, which is relational: translation, constraints and
`ExecuteUpdateAsync` are real. Behaviour that needs SQL Server is proven by the consumer against its
own migrated schema.

## Conventions

Line endings are LF, enforced by `.gitattributes`. Formatting is CSharpier, enforced on build; the
pinned version lives in `.config/dotnet-tools.json`.

An outcome a caller is expected to handle is a result code, not an exception — out of quota, an
unregistered unit, a denied read. Exceptions are for faults.

## Comments

Comments explain **why**, not what. The code says what it does; a comment that restates it is a
maintenance item that will drift out of date and mislead someone later. Prefer fixing the name over
adding the comment.
