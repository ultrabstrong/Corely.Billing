# Migrating to Corely.Billing 4.0

Nothing about billing behaves differently. Constants moved onto the types they describe, enums moved
into `Enums` namespaces, and three types no host used became internal. There is no database
migration.

The packages move together:

| Package | Version |
|---------|---------|
| Corely.Billing | 4.0.0 |
| Corely.Billing.IAM | 4.0.0 |
| Corely.Billing.Web | 5.0.0 |
| Corely.Billing.Web.IAM | 5.0.0 |
| Corely.Billing.DataAccessMigrations.Cli (`corely-billing-db`) | 4.0.0 |

## 1. Constants

The `Constants` namespaces and their classes are gone.

| Was | Now |
|-----|-----|
| `GrantConstants.NEVER_EXPIRES_UTC` | `Grant.NEVER_EXPIRES_UTC` |
| `GrantConstants.QUANTITY_MIN_VALUE`, `KIND_MAX_LENGTH` | Internal on `Grant` |
| `GrantRequestConstants.QUANTITY_MIN_VALUE`, `NOTE_MAX_LENGTH`, `DECISION_REASON_MAX_LENGTH`, `STATUS_MAX_LENGTH` | Internal on `GrantRequest` |
| `ConsumptionConstants.QUANTITY_MIN_VALUE`, `PROVIDER_MAX_LENGTH`, `IDEMPOTENCY_KEY_MAX_LENGTH`, `OUTCOME_MAX_LENGTH` | Internal on `ConsumptionEvent` |
| `RegistrationConstants.REFERENCE_MAX_LENGTH`, `DESCRIPTION_MAX_LENGTH` | Internal on `Registration` |

The limits are unchanged, and a request outside them still returns `ValidationError` with the limit in
its message.

## 2. Enums

| Enum | Was | Now |
|------|-----|-----|
| `GrantKind`, `GrantType` | `Corely.Billing.Grants.Models` | `Corely.Billing.Grants.Enums` |
| `GrantRequestStatus` | `Corely.Billing.GrantRequests.Models` | `Corely.Billing.GrantRequests.Enums` |
| `RegistrationStatus` | `Corely.Billing.Registrations.Models` | `Corely.Billing.Registrations.Enums` |
| `ConsumptionDimension`, `ConsumptionEventSortField`, `ConsumptionOutcome`, `TimeBucket` | `Corely.Billing.Consumption.Models` | `Corely.Billing.Consumption.Enums` |

Add the `using` for each namespace that names one of them, and the matching `@using` to the
`_Imports.razor` of any host page that does. Result code enums stay beside their results.

## 3. Types that became internal

`ValidationResult` and `ValidationError` in `Corely.Billing.Validators`, and `GrantTotalConsumptions`
in `Corely.Billing.Consumption.Models`, were public without any public member returning or taking
them. They are internal now.

## 4. Corely.Billing.Web, Corely.Billing.IAM and Corely.Billing.Web.IAM

Their own APIs do not change. They move to the next major because they carry Corely.Billing 4.0's
types to the host. The migration CLI moves to 4.0 because its major tracks Corely.Billing's.

Corely.Billing.IAM and Corely.Billing.Web.IAM now need Corely.IAM 6 and Corely.IAM.Web 8. Upgrade
IAM first with its own `MIGRATION-6.0.md`.

## 5. New: the Billing API

`UsageOperation.TryFrom` and `UsageUnit.TryFrom` parse without throwing, and Corely.Billing.Web.IAM
adds `MapBillingApiEndpoints()`, which serves quota, registrations, grants and consumption to a
caller holding an IAM token. Nothing changes for a host that does not map it. See
`Corely.Billing.Web.IAM/Docs/api.md`.
