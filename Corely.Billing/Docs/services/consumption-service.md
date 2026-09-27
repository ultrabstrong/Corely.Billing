# IConsumptionService

Reads the consumption ledger. Settled rows and holds still within their TTL count; released and expired holds do not.

## Methods

| Method | Parameters | Returns |
|--------|-----------|---------|
| `GetConsumptionTotalAsync` | `GetConsumptionTotalRequest` | `RetrieveSingleResult<long>` |
| `GetGrantConsumptionTotalsAsync` | `Guid accountId, IReadOnlyList<Guid> grantIds` | `RetrieveSingleResult<List<GrantTotalConsumptions>>` |
| `GetConsumptionTimeSeriesAsync` | `GetConsumptionTimeSeriesRequest` | `RetrieveSingleResult<List<ConsumptionSeries>>` |
| `ListConsumptionEventsAsync` | `ListConsumptionEventsRequest` | `RetrieveListResult<ConsumptionEvent>` |
| `ListProvidersAsync` | `Guid accountId` | `RetrieveSingleResult<List<string>>` |
| `GetEarliestConsumptionAsync` | `Guid accountId` | `RetrieveSingleResult<DateTime?>` |
| `CountAbandonedReservationsAsync` | `Guid accountId` | `RetrieveSingleResult<int>` |

## Usage

### Grant Balances

```csharp
var result = await consumptionService.GetGrantConsumptionTotalsAsync(accountId, [grantId]);
var used = result.Item?.SingleOrDefault()?.TotalConsumedQuantity ?? 0;
```

### Time Series

```csharp
var result = await consumptionService.GetConsumptionTimeSeriesAsync(
    new GetConsumptionTimeSeriesRequest(accountId, from, to, TimeBucket.Day, Units: [MyUsage.Token]));
```

Without `By`, one series whose `Key` is null: the total. With `By`, one series per operation, unit, provider or grant present in the range, keyed by its token, name or id, largest total first:

```csharp
var result = await consumptionService.GetConsumptionTimeSeriesAsync(
    new GetConsumptionTimeSeriesRequest(accountId, from, to, TimeBucket.Week,
        By: ConsumptionDimension.Provider, Units: [MyUsage.Token]));

foreach (var series in result.Item!)
    Console.WriteLine($"{series.Key}: {series.Total}");
```

Buckets are `Day`, `Week` and `Month`, in UTC. Every series carries every bucket in the range, zero where nothing was used. Different units are only ever added together if the request asks for several and no `By`, so filter to one unit, or split `By: ConsumptionDimension.Unit`, before charting a total.

### Event Listing

```csharp
var result = await consumptionService.ListConsumptionEventsAsync(new ListConsumptionEventsRequest(
    accountId, FromUtc: from, Providers: ["large-model"],
    SortBy: ConsumptionEventSortField.Quantity, SortDirection: SortDirection.Descending,
    Skip: 0, Take: 50));
```

Listing takes explicit filters rather than a `FilterBuilder`: operations and units are registered tokens, which a filter builder has no operation for. Without a sort, events list newest first.

## Notes

- `ConsumptionEvent.Outcome` is null while a hold is outstanding, then `Settled` or `Released`
- `ConsumptionEvent.CountsTowardBalance(liveFromUtc)` says whether a row counts, by the rule above; `IdempotencyScope` is the operation scope it was charged under, shared by every row of one charge
- A released row keeps its original quantity, so what was held stays visible in a dispute
- `CountAbandonedReservationsAsync` counts holds past their TTL that nothing resolved
