# UsageDashboard

`UsageChart` and `ConsumptionTable` under one row of range presets and filters.

## Usage

```razor
<UsageDashboard AccountId="accountId" DefaultRange="90d" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `DefaultRange` | `"30d"` | One of `7d`, `30d`, `90d`, `1y`, `all` |

## Filters

- **Range** — the presets, or any from and to date
- **All** — from the earliest usage or grant
- **Unit, operation, provider, grant** — multi-select; an empty selection means all
