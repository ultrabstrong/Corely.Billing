# UsageChart

Usage over a range, drawn one of six ways, one unit at a time. Under the time-based views, a second chart shows what each limited grant allowed while it was live; they are separate because a period's usage and a grant's total are different measures, and one axis would flatten the usage.

## Usage

```razor
<UsageChart AccountId="accountId" Filter="new UsageFilter(fromUtc, toUtc)" />

<UsageChart AccountId="accountId" Filter="filter"
            View="UsageChartView.Stacked" BreakDownBy="UsageBreakdown.Provider" ShowControls="false" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `Filter` | required | A `UsageFilter`: range, and optional units, operations, providers and grant ids |
| `View` | `Used` | Which view to open on |
| `BreakDownBy` | `Operation` | What Stacked, Side by side and Share split by: `Operation`, `Provider` or `Grant` |
| `ShowControls` | `true` | Show the view, breakdown and unit pickers |
| `ShowExport` | `true` | Show the CSV button |
| `Title` | `"Used"` | The Used view's title |

## Views

| View | Shows | Answers |
|---|---|---|
| Used | Total per period | How much, when? |
| Stacked | Per period, one segment per operation, provider or grant | What made up each period? |
| Side by side | Per period, one bar per operation, provider or grant | How do they compare, period to period? |
| Remaining | Each limited grant's balance at the end of each period, and the total across live grants | What was left, and when did it run low? |
| Burn-up | Use of the limited grants so far in the range, against the allowance live at each point | Is usage on pace to outlast the allowance? |
| Share | The range's total per operation, provider or grant, as ranked bars | Where did it go overall? |

## Behavior

- **One unit at a time**: the chart never adds two units together. When the range holds several, a picker lists the units with usage or a live grant, first in vocabulary order.
- **Buckets**: a day up to 31 days, a week up to 120, a month beyond
- **More than five series**: the rest fold into "Other"
- **Remaining and Burn-up** count every provider, since a balance does. Remaining reads each grant from its start, so the first period opens at the true balance. A balance never reads below zero; overdrawn grants show zero.
- **Capacity** (under Used, Stacked and Side by side): one stacked, stepped area per grant, three named, the rest folded into "Other grants"
- **Unlimited grants**: have no capacity or balance to draw; a note says one covers the range
- **Nothing to show**: an empty state instead of an empty chart
- **CSV**: downloads the series as drawn. See [Export](../export.md).

## Notes

- Chart.js is vendored at `_content/Corely.Billing.Web/lib/chart.js/`. The component's own module loads it.
- The event table is the chart's table view; pair them, as `UsageDashboard` does.
