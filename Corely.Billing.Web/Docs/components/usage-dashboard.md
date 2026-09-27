# UsageDashboard

`UsageChart` and `ConsumptionTable` under one row of range presets and filters, with an export of everything behind them.

## Usage

```razor
<UsageDashboard AccountId="accountId" DefaultRange="90d" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `DefaultRange` | `"30d"` | One of `7d`, `30d`, `90d`, `1y`, `all` |
| `ShowExport` | `true` | Show the Export button |

## Filters

- **Range**: the presets, or any from and to date
- **All**: from the earliest usage or grant
- **Unit, operation, provider, grant**: multi-select; an empty selection means all

## Export

**Export** downloads one zip for the current filter: every usage event, every grant, the chart's series, and a README describing each column and the rules behind them. It is meant to be handed to a spreadsheet, an analyst or an AI as it is. See [Export](../export.md).
