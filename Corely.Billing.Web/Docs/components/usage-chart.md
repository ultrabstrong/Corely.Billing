# UsageChart

Two charts on one time axis: what was used per period, and what each limited grant allowed while it was live. They are separate because a period's usage and a grant's total are different measures, and one axis would flatten the usage.

## Usage

```razor
<UsageChart AccountId="accountId" Filter="new UsageFilter(fromUtc, toUtc)" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `Filter` | required | A `UsageFilter`: range, and optional units, operations, providers and grant ids |
| `Title` | `"Used"` | The first chart's title |

## Behavior

- **Buckets** — a day up to 31 days, a week up to 120, a month beyond
- **Capacity** — one stacked, stepped area per grant, so a grant that starts or ends mid-range shows as a step
- **More than three grants** — the rest fold into "Other grants"
- **Unlimited grants** — have no capacity to draw; a note says one covers the range
- **Nothing to show** — an empty state instead of an empty chart

## Notes

- Chart.js is vendored at `_content/Corely.Billing.Web/lib/chart.js/`. The component's own module loads it.
- The event table is the chart's table view; pair them, as `UsageDashboard` does.
