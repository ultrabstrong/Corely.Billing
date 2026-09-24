# ConsumptionTable

Usage events for a range, newest first, with sortable columns and paging.

## Usage

```razor
<ConsumptionTable AccountId="accountId" Filter="filter" PageSize="50" />
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `AccountId` | required | The account |
| `Filter` | required | A `UsageFilter`; every field reaches `ListConsumptionEventsRequest` |
| `PageSize` | `25` | Rows per page |
| `Title` | `"Usage events"` | The heading |

## Notes

- Each column header sorts by that field; a second click reverses it.
- Status is Settled, Reserved or Released.
- A new `Filter` returns to the first page.
