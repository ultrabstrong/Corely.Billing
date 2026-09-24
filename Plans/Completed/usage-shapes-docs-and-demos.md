# Usage shapes: showing how widely the library applies

## Starting cold

For a session picking this up with no history.

**Read first:** this repository's `CLAUDE.md`, `DOCUMENTATION-STYLE.md`, and `Corely.Billing/Docs/`
(especially `reservations.md` and `services/`). Corely.IAM is the standard for every convention;
where this plan is silent, do what IAM does.

**The IAM precedent** under `C:\source\git\ultrabstrong\Corely.IAM\`: `Corely.IAM/Docs/usage-shapes.md`
(the page to model this one on), `Corely.IAM.Demos.UsersOnly` and `.SharedAccount` (demo host
shape, README style, `--seed`), and `Plans/Completed/simple-usage-shapes-docs-and-demos.md` (what
went wrong building them — LocalDB plus the migration tool, not SQLite with `EnsureCreated`).

**Code the gaps point at** (all under `Corely.Billing/`):

| Gap | Where |
|---|---|
| Unlimited quantity | `Grants/Models/Grant.cs`, `Grants/Entities/`, `Grants/Validators/GrantValidator.cs`, `Quota/Processors/ExpiringFirstGrantSelectionPolicy.cs`, `QuotaProcessor.RemainingRatio` |
| Active-grant query | `Grants/Processors/IGrantProcessor.ListActiveGrantsAsync` (exists, internal), `Services/IGrantService.cs` |

A schema change is a migration in both providers: `.\AddMigration.ps1 <Name>` from the repository
root, then the provider matrix (`$env:CORELY_RUN_CONTAINER_TESTS = "1"`, Docker running) to prove it
on SQL Server and MySQL.

**Ask the owner before building:** gaps 1 and 2 (the recommendations are written below), and
whether to build the IAM demo. Do not start the library changes until those are answered.

**The consumer.** DocsToData (`C:\source\git\pinnacleinnovation\DocsToData`) uses this library in
production-shaped code. A nullable `Quantity` is a breaking change for it: after release, DocsToData
bumps both `Corely.Billing` entries in `Directory.Packages.props`, regenerates
`iac/local/billing-schema.sql` with `corely-billing-db db script -i -p MsSql`, and handles null
where it reads `Quantity`. Note that follow-up in DocsToData rather than doing it from this session.

**Shipping it:** bump `<Version>` in `Corely.Billing/Corely.Billing.csproj`, and in the CLI's csproj
too if a migration was added (the CLI's major tracks the library's). Releasing is tagging `vX.Y.Z` on
`master`; ask before tagging, since a published version cannot be deleted.

**Running beside `web-components-and-demos.md`:** work on a branch or a separate `git worktree`, not
the shared checkout. Expect to merge `Corely.Billing.slnx`, the root `README.md`, `release.yml`,
`check-package-versions.sh` and `Corely.Billing/Docs/index.md`.

**Done when:** the library changes are tested on all three tiers including the provider matrix,
`RebuildAndTest.ps1` is green, the docs page is written, the demos are clicked through, and this plan
moves to `Plans/Completed/` with an Outcome section. Commit locally; push only when the owner says so.

## The problem

Corely.Billing reads as a metering system: overlapping grants, reservations, settlement, grant-edge
splitting, overdraft. Everything in the repository, and its one consumer, uses all of it. Someone
who needs something much simpler — "this account has an active one-year subscription" — would look
at the docs, see reservations and TTLs, and walk away, when the library already does most of what
they want.

Corely.IAM had the same problem and answered it with `Docs/usage-shapes.md` and two small demo apps
(`simple-usage-shapes-docs-and-demos`). Billing does the same.

## The shapes

| Shape | Grants | Consumption | Question the app asks |
|---|---|---|---|
| **Metered** (DocsToData) | Quantities that expire and overlap | Reserved before work, settled to the real cost | Is there room for this much work, and what did it cost? |
| **Subscription** | One grant per term, unlimited quantity | Not recorded, or one row per sign-in for activity | Does this account have a live grant right now? |
| **Prepaid credits** | A top-up pack per purchase, far-future expiry | Settled per use | How much is left, and when do I prompt a top-up? |
| **Trial** | A small grant with a short window | Settled per use | Has the trial run out, by time or by quantity? |
| **Seats or feature access** | One grant per feature, quantity = seats | Not recorded | Does the account have this feature, and how many? |

Each shape uses less of the library than the one above it, and the unused parts cost nothing: the
schema is the same, and a table with no rows is free.

## Gaps found while writing this

Checked against the code, not assumed. Two shapes need something the library does not offer yet.

### 1. An unlimited grant has no clean representation

`Grant.Quantity` is a `long`. The nearest thing to "unlimited" is `long.MaxValue`, and that breaks:

- `QuotaProcessor.RemainingRatio` sums the quantities of every live grant. Two `long.MaxValue`
  grants (a renewal overlapping the current term) overflow to a negative total, and the ratio is
  wrong.
- Every UI would render "9,223,372,036,854,775,807 pages".

Options:

- **(a) A sentinel constant**, `GrantConstants.UNLIMITED = long.MaxValue`, with every sum made
  overflow-safe and every display special-cased. No schema change. Every future sum is another place
  to forget.
- **(b) A nullable `Quantity`**, where null means unlimited. The selection policy treats a null grant
  as infinite room, the remaining ratio ignores it, and the UI prints "Unlimited". Needs a migration,
  which is cheap while the package is a preview.

**Recommendation: (b).** "Unlimited" is a real state, not a big number, and the type should say so.

### 2. "Is this account entitled right now?" has no public answer

- `IQuotaService.GetAvailabilityAsync` answers "is there room for one unit". That works for a
  subscription, but returns `Unknown` when the database cannot be reached, and callers are meant to
  let the work through. A subscription gate usually wants to fail closed instead.
- The "live grants at this instant" query exists, but only on the internal `IGrantProcessor`.
  `IGrantService.ListGrantsAsync` can filter by date with the filter builder, but not by operation
  or unit, because those are tokens the builder cannot compare.

**Recommendation:** add `IGrantService.ListActiveGrantsAsync(accountId, operation, unit)`, returning
the same `RetrieveListResult<Grant>` as the other lists, with failures reported as result codes. The
app decides whether an error means yes or no. The processor method is already written; this exposes
it.

### Not a gap: recording an event with no hold

Tracking sign-ins for a subscription is a reserve and a settle at quantity 1, under an operation
scope named after the session. That is two calls where one would do, but it works today and keeps
every row tied to a grant. Document the pattern. Only add a one-call convenience if the subscription
demo shows it to be awkward.

## Deliverables

### 1. `Corely.Billing/Docs/usage-shapes.md`

House style, modelled on IAM's page: the table above, then a short section per shape with the few
lines of code it needs. Linked from `Docs/index.md`, whose highlights and opening sentence stop
describing metering alone. The root README gets one line pointing at it, so nobody who only reads
the README misses it.

### 2. `Corely.Billing.Demos.Subscription`

The smallest possible host, and the counterweight to the metering demo:

- One page. "Subscribe for a year" creates an unlimited grant valid for a year. "Cancel" ends it by
  moving `ValidToUtc` to now.
- A gated page that opens only while `ListActiveGrantsAsync` returns a grant.
- An optional sign-in activity count: each visit to the gated page reserves and settles 1 under a
  scope per visit, and the page shows "visited N times this term".
- No reservations beyond that, no TTL tuning, no overdraft. The README states exactly which parts of
  the library the demo does not use, and why that is fine.

### 3. `Corely.Billing.Demos.WithIAM` — optional, decide when building

A metered host signed in through Corely.IAM, showing the two libraries together: the account id
from `UserContext.CurrentAccount`, authorization through `DecorateServices` and IAM permissions, both
schemas from their tools in one database. DocsToData already proves the combination works; the
question is whether a small public example earns its upkeep. Recommend it, after
`web-components-and-demos.md`, since it would use those components.

### 4. Tests

- Library changes (nullable quantity, `ListActiveGrantsAsync`) get unit tests, a SQLite integration
  test, and a provider-matrix case for the new query and for a null quantity in the balance and
  selection math.
- One functional smoke test per demo, as the portal demo has.

## Order

1. Decide gap 1 and gap 2. The rest depends on them.
2. The library changes, released as the next preview.
3. The docs page, which only needs the answers, not the demo.
4. The subscription demo.
5. The IAM demo, if chosen, after the web components ship.

## Out of scope

- Payments, renewals or dunning. A subscription here is a grant with dates. What creates the next
  one is the host's business.
- Proration, plan changes mid-term, or pricing of any kind.

## Outcome

Done, together with `web-components-and-demos.md`, which the owner folded into this work. Released as
1.0.0.

### Decisions the owner made

- **Gap 1: nullable `Quantity`, on the grant only.** Reservation, settlement, availability and the
  selection policy's inputs keep their shapes.
- **Gap 2: no new service method.** "Is this account entitled?" goes through the existing contract,
  `GetAvailabilityAsync`, and the selection policy was changed instead: live unlimited grants are
  spent first, then the old expiring-first order. A host that must fail closed treats `Unknown` as a
  refusal; the subscription demo does.
- **The IAM demo: built now**, along with the whole web plan.

### Library

- `Grant.Quantity`, `CreateGrantRequest.Quantity` and `UpdateGrantRequest.Quantity` are `long?`.
  Migration `NullableGrantQuantity` in both providers. `ExpiringFirstGrantSelectionPolicy` orders
  unlimited grants first and lets one take the whole charge. `RemainingRatio` reads 1 while one is
  live, where summing `long.MaxValue`s would have overflowed. Telemetry skips the quantity metric
  for an unlimited grant.
- Versions: the owner took Billing out of preview. `Corely.Billing`, the CLI and
  `Corely.Billing.Web` all ship as 1.0.0.
- Tests: unit (policy, validator, quota processor, telemetry decorator), a SQLite lifecycle test,
  and a provider-matrix case. Every new policy test was watched fail against the old policy. The
  matrix passes on SQL Server and MySQL.

### Found along the way

- **Corely.Common's `ComparableFilter` threw on every nullable property** except `IsNull` and
  `IsNotNull`: `FilterBuilder` has a `T?` overload, but the filter typed its constants as `T`. Making
  `Quantity` nullable broke Billing's own quantity-filter tests. Fixed in Corely.Common 2.0.3,
  released; Billing references it and the quantity-comparison tests are back.

### Docs and demos

- `Corely.Billing/Docs/usage-shapes.md`: all five shapes, linked from the docs index and README.
- `Corely.Billing.Demos.Subscription`: Razor Pages, the smallest host. Its smoke test drives
  subscribe, two visits, cancel, and the closed gate over HTTP. The one-call "record without a hold"
  convenience was not needed: the reserve-and-settle pair is two lines in `Membership.cs`.
- `Corely.Billing.Demos.WithIAM`: Corely.IAM 2.3.0 from NuGet, both schemas in one database from
  their own tools, and grants and usage authorized through `DecorateServices` against registered
  IAM resource types. Its authorization decorators are a working sketch for `iam-permissions-package.md`.

### Follow-ups

- DocsToData: `Plans/New/corely-billing-web-and-unlimited-grants.md` in that repository lists the
  package bump, the schema script, the null-quantity reads, and the move onto the components.
- Signed-in pages of the IAM demo were not clicked through in a browser: that needs a password
  typed into the sign-in form, which was left to the owner. Its anonymous paths are smoke-tested.
