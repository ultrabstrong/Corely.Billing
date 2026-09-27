# Usage charts that break usage down, and CSV export

**Status: decided, ready to build.** The owner settled every decision; the answers are under "Owner
decisions" at the end and are written into the body below. If something here turns out not to work,
stop and ask rather than choose.

## Starting cold

Read, in this order:

1. This repository's `CLAUDE.md` and `DOCUMENTATION-STYLE.md`.
2. `Corely.Billing.Web/Docs/components/` (`usage-chart.md`, `usage-dashboard.md`,
   `consumption-table.md`) for what ships today.
3. The chart as built: `Corely.Billing.Web/Components/UsageChart.razor`, `.razor.cs`, `.razor.js`,
   `UsageChartModel.cs`, `UsageFilter.cs`, and `UsageDashboard.razor` / `.razor.cs`.
4. The data behind it: `Corely.Billing/Services/IConsumptionService.cs`,
   `Corely.Billing/Consumption/Processors/ConsumptionReportProcessor.cs`
   (`GetConsumptionTimeSeriesAsync`, `ListConsumptionEventsAsync`) and
   `Corely.Billing/Consumption/Models/`.
5. `Plans/Completed/web-components-and-demos.md`, whose Outcome records why the chart looks the way it
   does: usage and capacity are two charts on one time axis, because two measures of different kinds
   should not share an axis, and the bar/line/area toggle was dropped.
6. Every decorator of `IConsumptionService`: the two telemetry decorators here, and
   `Corely.Billing.IAM/Authorization/ConsumptionAuthorizationDecorator.cs`, which must cover anything
   added to the interface.

Corely.IAM is the standard for conventions. The `Corely.Billing.Demos.Portal` demo is where every
view gets looked at.

**Done when:** the fix and every view below render in the Portal demo at desktop and phone widths,
light and dark; exports open cleanly in a spreadsheet; `RebuildAndTest.ps1` and the provider matrix
pass; docs are written; the packages are released; and this plan moves to `Plans/Completed/` with an
Outcome section.

## The problem

**The chart adds up different units.** `UsageChartModel.Build` plots `TotalQuantity` per bucket, and
the time series sums every row in the range whatever its unit. An account billed in tokens and in
images gets one bar per period reading "tokens plus images", which means nothing. The dashboard's
Unit filter hides it only when someone narrows to one unit. The Portal demo found it the moment it
used two units, and now uses one to stay clear of it.

**The chart answers one question.** It shows how much was used per period, in total. It cannot say
what made up that total (which operation, which provider, which grant), cannot compare those
side by side, cannot show what was left at each point in time, and cannot hand the data to someone
who wants it in a spreadsheet.

## What exists

- `GetConsumptionTimeSeriesAsync` returns `(BucketStart, TotalQuantity)` per bucket, with optional
  filters on units, operations, providers and grants. Nothing is split by any of those.
- The processor loads the matching rows (`UtcTimestamp`, `Quantity`) and buckets them in memory, so
  grouping by a further column costs one more selected field, not a new query shape.
- `UsageChart` draws two canvases: used per period (one bar series) and live capacity per grant
  (stepped, stacked, three named grants then "Other grants").
- `ConsumptionTable` pages events with the same filters and sorts them. It has no export.

## Deliverables

### 1. Never add different units together

The chart plots exactly one unit at a time, always. When the range holds several, a unit picker on
the chart lists only the units with data in the range, and defaults to the first in vocabulary order
(decision 1). The table keeps its multi-select Unit filter; rows name their own unit.

- Totals, axis labels and tooltips name the unit ("tokens"), from `IUsageVocabulary`.
- Capacity only draws grants of the plotted unit. It filters by the plotted unit already, but only
  when a Unit filter is set.
- A regression test renders two units with no unit filter and asserts they are never summed. Prove it
  catches the bug: run it against today's model, watch it fail, then fix.

### 2. The time series splits by a dimension

The one library change, made to the existing method rather than beside it (decision 2): the only
callers are this repository and DocsToData, so change the shape and fix what breaks.

```csharp
public enum ConsumptionDimension { Operation, Unit, Provider, Grant }

public sealed record GetConsumptionTimeSeriesRequest(
    Guid AccountId, DateTime FromUtc, DateTime ToUtc, TimeBucket Bucket,
    ConsumptionDimension? By = null,
    IReadOnlyList<UsageUnit>? Units = null, IReadOnlyList<UsageOperation>? Operations = null,
    IReadOnlyList<string>? Providers = null, IReadOnlyList<Guid>? GrantIds = null);

public sealed record ConsumptionSeries(string? Key, IReadOnlyList<ConsumptionTimeBucketData> Buckets);
```

`GetConsumptionTimeSeriesAsync` returns `List<ConsumptionSeries>`: with no `By`, one series whose
`Key` is null, the total as today; with `By`, one series per key present in the range. Every series
carries the same buckets, zero-filled. `Key` is the token, provider name, or grant id; display names
stay in the Web layer.

Fix every caller and decorator in the chain: service, telemetry decorator, processor, processor
telemetry decorator, Corely.Billing.IAM's decorator (Read on `consumption`, unchanged), the mock
repository path, `UsageChart`, and the integration tests on SQLite plus the SQL Server and MySQL
matrix. `consumption-service.md` and `result-codes.md` follow.

### 3. Views

One chart component, several views of the same filtered data. The dataviz rules the first pass
followed still hold: one measure per axis, a validated palette in light and dark, no pie charts,
series past a limit folded into "Other".

| View | Shows | Answers |
|---|---|---|
| Used | Total per period (today's chart) | How much, when? |
| Stacked | Per period, one segment per operation, provider or grant | What made up each period? |
| Side by side | Per period, one bar per operation, provider or grant | How do they compare, period to period? |
| Remaining | Each limited grant's balance at the end of each period, stepped, plus the total across live grants | What was left, and when did it run low? |
| Burn-up | Cumulative use across the range against the allowance live at each point | Is usage on pace to outlast the allowance? |
| Share | The range's total per operation, provider or grant, as ranked horizontal bars | Where did it go overall? |

- **Break down by** (Operation, Provider, Grant) applies to Stacked, Side by side and Share. Unit is
  never a breakdown, per deliverable 1.
- **Remaining** needs each grant's consumption before the range starts. Consumption cannot predate a
  grant, so a breakdown by grant from the earliest relevant grant's `ValidFromUtc` gives the opening
  balance and the per-period movement in one call. Overdrawn grants floor at zero with the overdraft
  named in the tooltip, as the grant list's balance meter does.
- **Capacity** stays the second canvas under Used, Stacked and Side by side, unchanged.
- **Component API:** `UsageChart` takes `View` and `BreakDownBy` parameters so a host can fix a view;
  `UsageDashboard` shows the pickers. Defaults keep today's output: Used, no breakdown.
- **Series limit:** five named series, the rest folded into "Other", ranked by the range's total.
  Needs palette tokens `--cbw-series-5` and `--cbw-series-6` beside the existing ones.

### 4. Export: the raw data, ready for analysis

The export exists so someone can hand their usage to an analyst, a spreadsheet, or their own AI and
ask for insights (decision 3). So it is the ledger, not a summary, with enough context that nobody has
to know Billing's rules to read it correctly.

**The dashboard's Export button downloads one zip** of the current filter and range:

`usage-events.csv`, one row per ledger row, holds and released rows included:

| Column | Why |
|---|---|
| `consumption_id` | Row identity, for joins and deduplication |
| `occurred_utc` | ISO 8601, UTC |
| `account_id` | So exports from several accounts can be merged |
| `operation`, `operation_name` | Token for joins, display name for people |
| `unit`, `unit_name` | As above |
| `quantity` | What the row charged or held |
| `status` | `settled`, `held`, `held_expired` or `released` |
| `counts_toward_balance` | `true` for settled rows and holds still within the TTL, so the reader needs no TTL rule to total a balance |
| `finalized_utc` | When a hold was settled or released |
| `provider` | Who did the work |
| `grant_id` | Which grant the row drew on; joins to `grants.csv` |
| `work_id` | The operation scope the row was charged under. One charge split across two grants is two rows with one `work_id` |
| `correlation_id` | Joins to the host's logs |
| `user_id` | Who asked, when the host passed it |
| `tags` | The row's tags as a JSON object |

`grants.csv`, every grant of the account, one row each: `grant_id`, `operation`, `operation_name`,
`unit`, `unit_name`, `quantity` (empty when unlimited), `unlimited`, `valid_from_utc`, `valid_to_utc`,
`status` at export time (`upcoming`, `active`, `expired`), `used`, `remaining` (empty when unlimited),
`overdrawn_by`, `tags` (JSON).

`chart-series.csv`: exactly what the chart draws in its current view, one row per period and series:
`period_start_utc`, `period`, `series_key`, `series_name`, `quantity`, `unit`.

`README.md`: a data dictionary. What each file and column means, the status and balance rules, that
an empty quantity is unlimited, that times are UTC, the filter and range the export was taken with,
when it was taken, and whether it was capped. A model given bare CSVs guesses at all of these.

**The components export on their own too,** for hosts that compose their own pages:
`ConsumptionTable` downloads `usage-events.csv`, `GrantList` downloads `grants.csv`, and `UsageChart`
downloads `chart-series.csv`, each with the same columns as the zip.

How:

- `UsageExport` builds the files and the zip, a conversion with its own seam and tests, per
  `CLAUDE.md`. `System.IO.Compression` for the zip, no new dependency. RFC 4180 quoting, invariant
  culture numbers, UTF-8 with a byte order mark so Excel reads it.
- `work_id` is the idempotency scope, which the ledger stores only inside `IdempotencyKey`
  (`{scope}|{operation}|{unit}|{grantId:N}`). Parse it out in one place with its own tests, or expose
  the scope on `ConsumptionEvent` if the parse proves fragile; do not guess at it in the exporter.
- **Formula injection:** a cell starting with `=`, `+`, `-`, `@`, a tab or a carriage return gets a
  leading `'`. Providers and tags are strings a caller supplied, and a spreadsheet would run them.
- Events are read in pages of 1,000 through `ListConsumptionEventsAsync` and streamed to the browser
  through a `DotNetStreamReference` and a small function in the component's JS module. No new
  dependency, nothing inline, so it passes a strict Content Security Policy (DocsToData's browser tests
  fail on any violation, so check it there).
- Reading consumption and grants is already authorized by the service decorators, so an export needs
  no new permission. What a caller may not read, it does not get.
- **Capped at 100,000 events** (decision 4). The download still happens; afterwards the component
  shows a message beside the button naming it, such as "This export stopped at 100,000 of 134,210
  events. Narrow the date range to get the rest.", and the README says the same.

### 5. Tests

- **Library:** unit tests for the breakdown processor and decorators (Corely.Billing.IAM's included),
  integration tests for the breakdown on SQLite and in the provider matrix.
- **Web (bUnit):** each view's model from known buckets, the fold into "Other", the unit rule,
  Remaining's opening balance and overdraft floor, every export column, `work_id` parsing, quoting,
  injection escaping, and the capped message.
- **Functional:** the demo still starts and serves the chart module.

### 6. Docs and demos

- `usage-chart.md` and `usage-dashboard.md`: each view with the question it answers, the parameters,
  and the zip export with every column. `consumption-table.md` and `grant-list.md`: their own export.
  The core `consumption-service.md`: the `By` dimension and the series result.
- **Portal demo:** bring the second unit back (image generation, billed per image) now that the chart
  handles it, and seed enough operations and providers for Stacked and Share to be worth looking at.

### 7. Release

All of it at once (decision 5). The time series change breaks its signature, so by semver:
Corely.Billing 3.0.0, Corely.Billing.Web 3.0.0, Corely.Billing.IAM 2.0.0. Corely.Billing.Web.IAM and
the CLI move only if their own code changes; there is no schema change. DocsToData takes the new
versions afterwards, in its own repository, and fixes whatever the time series change breaks there.

## Out of scope

- A projected run-out date. Burn-up shows the pace; predicting the date is a Feature-Ideas item.
- Money, prices or invoices. Billing counts units.
- Scheduled or emailed exports, and any format but CSV.
- Moving the bucketing into SQL. It loads matching rows into memory today, which is fine for the
  ranges the dashboard offers; revisit if an account's year runs to millions of rows.

## Owner decisions

Settled with the owner; the body above is written to match.

1. **One unit per chart, chosen by a picker.** It lists only units with data in the range and
   defaults to the first in vocabulary order.
2. **Change the existing time series, do not add a method beside it.** The library is days old and
   its only callers are this repository and DocsToData, so shape it right and fix what breaks.
3. **Export the raw data someone would want for their own analysis.** Every ledger row with the grant
   it drew on, a separate grants file, the chart's series, and a README data dictionary, zipped from
   the dashboard; each component also exports its own file.
4. **Cap exports at 100,000 events, and say so.** The capped file still downloads, and the UI tells
   the user it was capped and how to get the rest.
5. **Ship everything at once.** No separate fix release.
