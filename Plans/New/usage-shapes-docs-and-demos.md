# Usage shapes: showing how widely the library applies

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
