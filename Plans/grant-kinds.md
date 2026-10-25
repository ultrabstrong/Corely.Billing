# Grant kinds: consumable, capacity and allowance

**Status: designed 2026-10-24 with the owner, ready to build.** A major version of `Corely.Billing`
and the packages that depend on it. Found by dogfooding: DocsToData's document retention needed a
grant that adds days rather than one that is burned down, and the library could only store, read and
show the burned down kind.

## Ground rule

**Built as intended, not around what is deployed.** Nothing outside the owner's own apps uses these
packages, so method shapes, request types, components and the schema change outright where a better
design calls for it. A migration guide at the repository root covers the move, as for every major
version. No compatibility shims, no obsolete members kept alongside.

## The problem

Every grant today is a balance: usage reserves and settles against it, and the components draw a
meter. Three other shapes of entitlement come up as soon as an app does more than meter:

- **Seats:** a limit that things take and give back (users, active workflows, a subscription's
  members). Nothing is used up; a seat is held until released.
- **An amount the app reads:** extra retention days, a larger file size limit. No usage at all.
- **A feature:** on or off (SFTP access, a provider).

Today these are approximated with consumable grants (`Docs/usage-shapes.md`'s "Seats or feature
access" and "Subscription" rows), and the components show them as balances: DocsToData's retention
grant reads "60 days left of 60". The forms also offer every registered operation with every
registered unit, so "Document Extraction in days" can be granted.

## The design

### Three kinds, stored on the grant

`GrantKind`, a new required column on `Grants`:

| Kind | The quantity is | Usage against it |
|---|---|---|
| `Consumable` | A balance | Reserved and settled, as today |
| `Capacity` | A number of seats | Registered and released (below) |
| `Allowance` | An amount the app reads | None |

**The row is the truth.** A grant says what kind it is, so any reader (a component, a report, a
query) can tell without the app's registrations. Existing rows migrate to `Consumable`, which is
what every grant is today; the model has no default.

### The display type, read from the row

`GrantKind` and whether `Quantity` is set give six display types, a `GrantType` enum with no storage
of its own:

```csharp
public GrantType GetGrantType() =>
    (Kind, Quantity) switch
    {
        (GrantKind.Consumable, null) => GrantType.Unlimited,
        (GrantKind.Consumable, _) => GrantType.Balance,
        (GrantKind.Capacity, null) => GrantType.UnlimitedSeats,
        (GrantKind.Capacity, _) => GrantType.Seats,
        (GrantKind.Allowance, null) => GrantType.Feature,
        (GrantKind.Allowance, _) => GrantType.Allowance,
    };
```

A feature is an allowance with no quantity: any active grant means yes, none means no. No magic
numbers.

### Operations declare their kind and unit

The vocabulary (`IUsageVocabulary`, `BillingOptions.RegisterOperation`) already lists operations and
units for display. Each operation now also names its kind and its unit:

```csharp
billing
    .RegisterUnit("pages", "page")
    .RegisterUnit("days", "day")
    .RegisterOperation("document_extraction", "Document Extraction", GrantKind.Consumable, unit: "pages")
    .RegisterOperation("document_retention", "Document Retention", GrantKind.Allowance, unit: "days")
    .RegisterOperation("seats", "Seats", GrantKind.Capacity, unit: "users")
    .RegisterOperation("sftp_access", "SFTP Access", GrantKind.Allowance);

public sealed record UsageOperationDefinition(
    UsageOperation Operation, string DisplayName, GrantKind Kind, UsageUnit? Unit);
```

The declaration is a guard, not the source: creating a grant whose kind or unit disagrees with its
operation's declaration is refused with a result code, and the forms offer only declared pairs. A
grant whose operation is no longer declared can still be read, because its kind is on the row. Units
keep their own registration for display names, since several operations can share one. No new
interface: the vocabulary stays the shared list of names grants, consumption and quota use.

### Registrations, for capacity

A new table, one row per seat held:

```text
RegistrationId  AccountId  GrantId   Reference   Description     RegisteredUtc     ReleasedUtc
019b...1        019d...a   019a...3  user:alice  Alice (admin)   2026-11-02 09:00  null
019b...2        019d...a   019a...3  user:bob    null            2026-11-02 09:05  2026-11-20 14:00
```

- **A registration points at the grant it holds,** chosen when it is made, as consumption picks a
  grant. `RegistrationSelectionPolicy` picks the **longest lived** active grant with room, the
  opposite of consumables (soonest expiring first), so a seat does not sit on a grant ending next
  week.
- **`Reference` is optional and unique among an account's active registrations for an operation,**
  so registering the same user twice is `AlreadyRegistered`, not a second seat. `Description` is free
  text.
- **Registering is atomic against the capacity.** Two registrations racing for the last seat cannot
  both succeed.
- **Nothing about a registration's state is stored beyond `ReleasedUtc`.** Its status is read from
  its grant at query time: `Active`, `Released`, `GrantEnded`, `GrantDisabled`.
- **A registration stays on its grant when the grant ends or is disabled.** It is reported as such,
  never dropped or moved silently. Moving is explicit (`MoveRegistrationsAsync`): each move releases
  the old registration and creates a new one on the target grant, for one, some or all of a grant's
  registrations.

### Quota reads one per kind, shaped alike

`IQuotaService` answers each kind with one read, and capacity with its writes:

```csharp
public interface IQuotaService
{
    Task<GetBalanceResult> GetBalanceAsync(Guid accountId, UsageOperation operation, CancellationToken ct = default);
    Task<ReserveQuotaResult> ReserveAsync(ReserveQuotaRequest request, CancellationToken ct = default);
    Task<SettleQuotaResult> SettleAsync(SettleQuotaRequest request, CancellationToken ct = default);
    Task<SettleQuotaResult> ReleaseAsync(ReleaseQuotaRequest request, CancellationToken ct = default);

    Task<GetAllowanceResult> GetAllowanceAsync(Guid accountId, UsageOperation operation, CancellationToken ct = default);
    Task<HasFeatureResult> HasFeatureAsync(Guid accountId, UsageOperation operation, CancellationToken ct = default);

    Task<GetCapacityResult> GetCapacityAsync(Guid accountId, UsageOperation operation, CancellationToken ct = default);
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<ModifyResult> ReleaseRegistrationAsync(Guid accountId, Guid registrationId, CancellationToken ct = default);
    Task<MoveRegistrationsResult> MoveRegistrationsAsync(MoveRegistrationsRequest request, CancellationToken ct = default);
    Task<RetrieveListResult<Registration>> ListRegistrationsAsync(ListRegistrationsRequest request, CancellationToken ct = default);
}

public record GetBalanceResult(GetBalanceResultCode ResultCode, long? Remaining, IReadOnlyList<Grant> Grants);
public record GetAllowanceResult(GetAllowanceResultCode ResultCode, long? Total, IReadOnlyList<Grant> Grants);
public record GetCapacityResult(GetCapacityResultCode ResultCode, long? Granted, long InUse, IReadOnlyList<Grant> Grants);

public record RegisterRequest(Guid AccountId, UsageOperation Operation, string? Reference = null, string? Description = null);
public record RegisterResult(RegisterResultCode ResultCode, Registration? Registration);
public enum RegisterResultCode { Registered, AlreadyRegistered, NoCapacity, NoActiveGrant }

public record MoveRegistrationsRequest(Guid AccountId, Guid FromGrantId, Guid ToGrantId, IReadOnlyList<Guid>? RegistrationIds = null);

public record Registration(Guid RegistrationId, Guid AccountId, Guid GrantId, string? Reference,
    string? Description, DateTime RegisteredUtc, DateTime? ReleasedUtc, RegistrationStatus Status);
public enum RegistrationStatus { Active, Released, GrantEnded, GrantDisabled }
```

- `Remaining`, `Total` and `Granted` are `null` for unlimited. Several active grants of one operation
  add up: allowances and capacities sum; consumables are drawn soonest expiring first, as today.
- Each read refuses a grant of the wrong kind for its operation with a result code, never an
  exception.
- `HasFeatureAsync` is shorthand for an allowance that is unlimited or above zero. It exists because
  apps think "does this account have it", and the helpers mirror how apps use grants rather than how
  they are stored.

### Grants carry their own state

- **`CreateGrantRequest` gains `Kind`,** checked against the operation's declaration.
- **The `Grant` that `ListGrantsAsync` and `GetGrantAsync` return carries its state:** `Used` and
  `Remaining` for a consumable, `InUse` for a capacity. The list shows every grant without a second
  call or arithmetic of its own.
- **`ListGrantsRequest` gains filters:** kind, operation, active on a date, and account ids.

### A wider read surface, writes kept narrow

The library kept running into hosts that could not see into it. Reads open up; writes do not.

- **Writes stay narrow.** Reserve, settle, register and the ledger have invariants (never charged
  past a quantity, never two seats for one reference), so they stay behind purpose built calls.
- **Reads become a general query surface:** grants, registrations, consumption and grant requests,
  each filterable by account, operation, kind and date, and paged.
- **Reads go through the same permission checks,** plus a **cross-account read** for a platform role
  that holds it: more than the current account in `ListGrantsRequest.AccountIds`. In
  `Corely.Billing.IAM` that is a permission a platform role is given, as `grant` actions are today.
  This is what lets a platform owner see every account's allowances, which DocsToData's retention
  warning needs.
- **Not raw tables.** No `IQueryable` or `DbContext` reaches a host: that would skip permissions and
  break every host on a schema change.

### Replaced, not kept beside

| Today | Replaced by | Why |
|---|---|---|
| `IQuotaService.GetAvailabilityAsync` and the `QuotaAvailability` enum | `GetBalanceAsync` | An enum with no amounts. The three kinds get one read each, shaped alike |
| `IConsumptionService.GetGrantConsumptionTotalsAsync` | `Used` and `Remaining` on `Grant` | It exists so the grant list can work out each grant's state itself |
| Operations and units registered with no link | Each operation names its unit | The cause of "Document Extraction in days" |

Kept: `ReserveAsync`, `SettleAsync`, `ReleaseAsync`, and the consumption reporting reads (totals,
time series, events, providers), which serve usage charts rather than grant state.

## The components

Every grant component switches on `GrantType`.

- **The grant list:**
  - Balance: the meter, "320 of 500 pages left".
  - Unlimited: "Unlimited, 1,240 pages used".
  - Seats: "2 of 7 seats in use".
  - Unlimited seats: "Unlimited, 12 in use".
  - Allowance: "+60 days, until 1 June 2027".
  - Feature: "Enabled, until 1 June 2027".
- **The grant editor and the request form, as one flow:**
  1. Pick what to grant, from operations grouped by type (Balance: Document Extraction; Allowance:
     Document Retention; Feature: SFTP Access).
  2. The form shows only that type's fields. Balance and Seats: quantity (or unlimited), start, end
     with a "never expires" switch. Allowance: the amount added, start, end. Feature: start and end
     only.
  3. The unit is never asked for: it comes from the operation.

  The request form a customer uses follows the same steps, limited to what they may request.
- **A host hook before disabling a grant,** so a host can show what disabling costs ("14 jobs lose
  their files") above the editor. DocsToData already does this from its page shell; check whether a
  hook still earns its place once the components are rebuilt.
- **Registrations:** a list for a capacity grant (reference, description, since, status), with
  release and move for those allowed to.

## The docs

Every way of using grants gets a page, with the quota helpers it uses, so an app works with "register
a seat" or "what is this account's retention", never with the storage underneath. `usage-shapes.md`
is rewritten around the kinds:

| Shape | Kind | What the app calls |
|---|---|---|
| Metered | Consumable | `ReserveAsync`, `SettleAsync` |
| Prepaid credits | Consumable, far future expiry | `GetBalanceAsync`, `SettleAsync` |
| Trial | Consumable, short window | `GetBalanceAsync` |
| Seats | Capacity | `RegisterAsync`, `ReleaseRegistrationAsync`, `GetCapacityAsync` |
| **Subscription** | Capacity with no expiry | Register members as seats. The app tracks whether the subscription is paid: a lapse disables the grant, a renewal enables it again, so its registrations come back with it |
| An extra amount | Allowance | `GetAllowanceAsync`: the host adds the total to its own default |
| Feature access | Allowance with no quantity | `HasFeatureAsync` |

The subscription page says plainly that the grant never expires and that disabling is the lapse,
since "a subscription that never ends" reads as a contradiction otherwise. The demos follow:
`Corely.Billing.Demos.Subscription` moves to a capacity grant.

## Tests

Each tier as the repository defines them, and each regression test proven red against the bug it
guards.

- `GetGrantType` for all six combinations (unit).
- Creating a grant whose kind or unit disagrees with its operation is refused (unit).
- Allowances and capacities sum across active grants; disabled, expired and future grants are
  ignored; unlimited wins (integration).
- Registering: the longest lived grant with room is picked; the last seat cannot be taken twice by
  two concurrent registrations; a reference already active is `AlreadyRegistered` (integration,
  against SQL Server).
- A registration on an ended or disabled grant reports that status; moving releases and recreates
  (integration).
- Cross-account reads: refused without the platform permission, allowed with it (IAM package tests).
- The components: each `GrantType` renders its own way, and the forms offer only declared pairs
  (Web unit tests).
- The migration: existing grants come out `Consumable` (migration tests).

## Releasing

Every changed package takes a major version, released in dependency order on the owner's say-so,
one tag at a time: `Corely.Billing`, then `Corely.Billing.IAM`, `Corely.Billing.Web`,
`Corely.Billing.Web.IAM` and `Corely.Billing.DataAccessMigrations.Cli`. A migration guide at the
repository root lists each replaced member and what replaces it.

## Then, in DocsToData

Document retention (`Plans/InProgress/document-retention.md` there, built and committed locally as
`e71b2cd`) moves to the new version:

- Its grant is `Allowance`, `document_retention`, in days; the period is the platform default plus
  `GetAllowanceAsync`'s total.
- The warning before lowering the platform default counts every account's retention grants through
  the cross-account read, instead of the default alone.
- The grants page shows retention as an allowance, and the forms stop offering mismatched pairs.
- Admission checks move from `GetAvailabilityAsync` to `GetBalanceAsync`.
