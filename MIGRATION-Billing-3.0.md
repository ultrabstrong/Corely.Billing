# Migrating to Corely.Billing 3.0

Every grant now has a kind: `Consumable`, `Capacity`, `Allowance` or `Feature`. Consumable is what every grant was before. The other three are new: a capacity that registrations hold and release, an amount the app reads, and a feature that is on or off. Each operation declares its kind and unit, and each kind has its own quota read.

The packages move together:

| Package | Version |
|---------|---------|
| Corely.Billing | 3.0.0 |
| Corely.Billing.IAM | 3.0.0 |
| Corely.Billing.Web | 4.0.0 |
| Corely.Billing.Web.IAM | 4.0.0 |
| Corely.Billing.DataAccessMigrations.Cli (`corely-billing-db`) | 3.0.0 |

## 1. Migrate the database

Run `corely-billing-db` 3.0 against each database:

```bash
corely-billing-db db migrate -p MsSql -c "<connection string>"
```

Migration `AddGrantKindsAndRegistrations` adds `Grants.Kind` and `GrantRequests.Kind` and sets every existing row to `Consumable`, makes `Unit` nullable on both (null only for a feature), makes `GrantRequests.Quantity` nullable, and adds the `Registrations` table. Existing grants read exactly as before.

## 2. Declare each operation's kind and unit

`RegisterOperation` takes the kind, and the unit for every kind but a feature:

```csharp
billing
    .RegisterUnit("token", "token")
    .RegisterOperation("text_generation", "Text Generation", GrantKind.Consumable, "token");
```

`AddBillingServices` throws at startup when an operation names an unregistered unit, a feature names a unit, or any other kind names none. An operation that was registered with no unit before, and whose grants used several units, needs one operation per unit.

## 3. Grants

| Was | Now |
|-----|-----|
| `CreateGrantRequest(AccountId, Operation, Unit, Quantity, ValidFromUtc, ValidToUtc, Tags)` | `CreateGrantRequest(AccountId, Operation, Kind, Quantity, ValidFromUtc, ValidToUtc, Unit = null, Tags = null)`; a null unit takes the operation's |
| `UpdateGrantRequest(AccountId, GrantId, Unit, Quantity, ValidFromUtc, ValidToUtc, Tags)` | `UpdateGrantRequest(AccountId, GrantId, Quantity, ValidFromUtc, ValidToUtc, Tags)`; a grant's unit never changes |
| `ListGrantsRequest(AccountId, Filter, Order, Skip, Take)` | `ListGrantsRequest(AccountIds, Kind, Operation, ActiveAtUtc, Filter, Order, Skip, Take)` |
| `Grant.Unit` (`UsageUnit`) | `Grant.Unit` (`UsageUnit?`), null for a feature |
| None | `Grant.Kind`, `GetGrantType()`, `NeverExpires()`, and the state `Used`, `Remaining`, `InUse` |
| `IConsumptionService.GetGrantConsumptionTotalsAsync` | `Grant.Used` and `Grant.Remaining`, filled by `GetGrantAsync` and `ListGrantsAsync` |
| None | `CreateGrantResultCode.DeclarationMismatchError` |

## 4. Quota

| Was | Now |
|-----|-----|
| `GetAvailabilityAsync(accountId, operation, unit)` and `QuotaAvailability` | `GetBalanceAsync(accountId, operation)`: `Remaining` (null for unlimited) and `HasAny` |
| `QuotaAvailability.Unknown` when the database was unreachable | The read throws, as any fault does |
| `QuotaAvailability.Unauthorized` | `QuotaReadResultCode.UnauthorizedError` |
| None | `GetAllowanceAsync`, `HasFeatureAsync`, `GetCapacityAsync` |
| None | `RegisterAsync`, `ReleaseRegistrationAsync`, `MoveRegistrationsAsync`, `ListRegistrationsAsync` |
| `DeleteGrantAsync` | Also releases a capacity grant's registrations |

A gate that compared against `QuotaAvailability.Available`:

```csharp
var open = (await quotaService.GetBalanceAsync(accountId, op)).HasAny;
```

Each read returns `WrongKindError` for an operation declared as another kind. A grant that stood in for a capacity or a feature before, as a consumable, can become a `Capacity` or `Feature` grant of a new operation. See [Usage Shapes](Corely.Billing/Docs/usage-shapes.md).

## 5. Grant requests and consumption

| Was | Now |
|-----|-----|
| `RequestGrantRequest(AccountId, Operation, Unit, Quantity, Note, RequestedByUserId, Tags)` | `RequestGrantRequest(AccountId, Operation, Quantity, Note, RequestedByUserId, Tags)`; kind and unit come from the operation, quantity is null for a feature |
| `ListGrantRequestsRequest(AccountId, Filter, Order, Skip, Take)` | `ListGrantRequestsRequest(AccountIds, Operation, Status, Filter, Order, Skip, Take)` |
| One pending request per operation and unit | One pending request per operation |
| `ListConsumptionEventsRequest(AccountId, ...)` | `ListConsumptionEventsRequest(AccountIds, ...)` |

## 6. Corely.Billing.IAM

- **Cross-account reads**: a list with more than one account id is allowed for the system context, or for a user in the platform account holding Read on the new `billing_all_accounts` resource type and Read on the list's own type. Grant a platform role both to read every account. No owner holds `billing_all_accounts`.
- **Quota**: the four reads need Read on `quota`; registering, releasing and moving registrations need Execute, as reserve and settle do; listing registrations needs Read.

## 7. Corely.Billing.Web and Corely.Billing.Web.IAM

- **`GrantList`** groups grants by kind under a heading each, and shows each grant by its type in a State column; only a balance draws a meter. It no longer reads consumption, so a caller without Read on consumption sees balances too.
- **`GrantEditor`** and **`GrantRequestForm`** group operations by kind, ask only for that kind's fields, and never ask for a unit. The editor lists a capacity grant's registrations.
- **`RegistrationList`** is new: what is registered against a capacity operation, across its grants or on one, with Release and Move.
- **`GrantAction.ManageRegistrations`** is new. A host's own `IGrantActionGate` must answer it; Corely.Billing.Web.IAM maps it to Execute on `quota`.
- **`grants.csv`** gains `kind`, `type` and `in_use`, and drops `overdrawn_by`.
