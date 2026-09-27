# Usage charts that break usage down, and CSV export

**Status: draft.** The owner decisions at the end are open. Take them to the owner one at a time,
with the recommendation, and write the answers in before building.

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

The chart plots exactly one unit at a time, always. Decision 1 settles how the unit is chosen when
the range holds several. Whichever it is:

- Totals, axis labels and tooltips name the unit ("tokens"), from `IUsageVocabulary`.
- Capacity only draws grants of the plotted unit. It filters by the plotted unit already, but only
  when a Unit filter is set.
- A regression test renders two units with no unit filter and asserts they are never summed. Prove it
  catches the bug: run it against today's model, watch it fail, then fix.

### 2. A breakdown series in Corely.Billing

The one library change. A time series split by one dimension:

```csharp
public enum ConsumptionDimension { Operation, Unit, Provider, Grant }

public sealed record GetConsumptionBreakdownRequest(
    Guid AccountId, DateTime FromUtc, DateTime ToUtc, TimeBucket Bucket,
    ConsumptionDimension By,
    IReadOnlyList<UsageUnit>? Units = null, IReadOnlyList<UsageOperation>? Operations = null,
    IReadOnlyList<string>? Providers = null, IReadOnlyList<Guid>? GrantIds = null);

public sealed record ConsumptionSeries(string Key, IReadOnlyList<ConsumptionTimeBucketData> Buckets);
```

`IConsumptionService.GetConsumptionBreakdownAsync(request)` returns one `ConsumptionSeries` per key
present in the range, every series with the same buckets, zero-filled, as the total series is. `Key`
is the token, provider name, or grant id; display names stay in the Web layer.

Through the whole chain, as every service method goes: service, telemetry decorator, processor,
processor telemetry decorator, Corely.Billing.IAM's decorator (Read on `consumption`, exactly as the
time series), the mock repository path, and the integration tests on SQLite plus the SQL Server and
MySQL matrix. Decision 2 is whether this is a new method or a field on the existing request.

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

### 4. CSV export

Two exports, each a button beside what it exports:

- **Usage events,** from `ConsumptionTable`: every row matching the current filter and sort, not just
  the page on screen. Columns: when (UTC, ISO 8601), quantity, unit, operation, provider, status,
  grant id. Tokens, not display names, so a re-import matches; decision 3 covers adding display names.
- **Chart data,** from `UsageChart`: exactly the series drawn in the current view, one row per period
  and series (period start UTC, series, quantity, unit).

How:

- A `ConsumptionCsv` type builds the rows, a conversion with its own seam and tests, per `CLAUDE.md`.
  RFC 4180 quoting, invariant culture numbers, UTF-8 with a byte order mark so Excel reads it.
- **Formula injection:** a cell starting with `=`, `+`, `-`, `@`, a tab or a carriage return gets a
  leading `'`. Providers and tags are strings a caller supplied, and a spreadsheet would run them.
- Events are read in pages of 1,000 through `ListConsumptionEventsAsync` and streamed to the browser
  through a `DotNetStreamReference` and a small function in the component's JS module. No new
  dependency, nothing inline, so it passes a strict Content Security Policy (DocsToData's browser tests
  fail on any violation, so check it there).
- Reading consumption is already authorized by the service decorators, so an export needs no new
  permission. Decision 4 is the row cap.

### 5. Tests

- **Library:** unit tests for the breakdown processor and decorators (Corely.Billing.IAM's included),
  integration tests for the breakdown on SQLite and in the provider matrix.
- **Web (bUnit):** each view's model from known buckets, the fold into "Other", the unit rule,
  Remaining's opening balance and overdraft floor, the export's rows, quoting and injection escaping.
- **Functional:** the demo still starts and serves the chart module.

### 6. Docs and demos

- `usage-chart.md` and `usage-dashboard.md`: each view with the question it answers, the parameters,
  and export. `consumption-table.md`: export. The core `consumption-service.md`: the breakdown.
- **Portal demo:** bring the second unit back (image generation, billed per image) now that the chart
  handles it, and seed enough operations and providers for Stacked and Share to be worth looking at.

### 7. Release

Minor versions: Corely.Billing 2.1.0, Corely.Billing.Web 2.1.0, Corely.Billing.IAM 1.1.0 (it must
decorate the new method, or the method would skip authorization). Corely.Billing.Web.IAM and the CLI
do not change; no schema change. Ask before tagging. DocsToData takes the new versions afterwards, in
its own repository.

## Out of scope

- A projected run-out date. Burn-up shows the pace; predicting the date is a Feature-Ideas item.
- Money, prices or invoices. Billing counts units.
- Scheduled or emailed exports, and any format but CSV.
- Moving the bucketing into SQL. It loads matching rows into memory today, which is fine for the
  ranges the dashboard offers; revisit if an account's year runs to millions of rows.

## Owner decisions

1. **Choosing the unit when the range holds several.** (a) A unit picker on the chart listing only
   units with data in the range, defaulting to the first in vocabulary order; (b) one chart per unit,
   stacked vertically. Recommend (a): (b) multiplies every view, and most accounts bill one unit.
2. **New method or a field on the existing request.** Adding `By` to
   `GetConsumptionTimeSeriesRequest` changes its constructor and its result shape, which breaks
   compiled callers. A new `GetConsumptionBreakdownAsync` is additive for callers, but still a new
   interface member, which breaks anyone implementing `IConsumptionService` outside this repository.
   Recommend the new method, released as a minor version with that noted, since Corely.Billing.IAM is
   the only known outside implementer and ships in the same release.
3. **Display names in the events export.** Tokens only, or tokens plus a display name column for
   operation and unit. Recommend both: tokens for re-import, names for people.
4. **Export row cap.** Recommend 100,000 rows, with the button saying so when the filter matches more,
   so a click cannot pull an unbounded ledger into server memory. The alternative is no cap, trusting
   that streaming keeps memory flat.
5. **Ship the unit fix first.** Recommend yes: deliverable 1 alone as Corely.Billing.Web 2.0.1, since
   today's chart is wrong for any multi-unit host, then the rest as 2.1.0.
