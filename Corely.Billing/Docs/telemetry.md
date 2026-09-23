# Telemetry

`IBillingTelemetry` receives counters and measurements from the processors. The default discards them; supply an implementation to send them to your metrics system.

## Usage

```csharp
internal class OpenTelemetryBillingTelemetry(Meter meter) : IBillingTelemetry
{
    public void Increment(string metric) => meter.CreateCounter<long>(metric).Add(1);
    public void Record(string metric, double value) => meter.CreateHistogram<double>(metric).Record(value);
}
```

```csharp
var options = BillingOptions.Create(configuration, efConfigFactory)
    .RegisterOperation("document_extraction", "Document Extraction")
    .RegisterUnit("page", "page")
    .UseTelemetry(sp => new OpenTelemetryBillingTelemetry(sp.GetRequiredService<Meter>()));
```

Metric names carry no prefix. Add one in the implementation if your dashboards need it.

## Metrics

| Constant | Name | Kind | Recorded when |
|----------|------|------|---------------|
| `Quota.GRANT_FOUND` | `quota.lookup.grant_found` | Count | A reservation succeeds |
| `Quota.GRANT_NOT_FOUND` | `quota.lookup.grant_not_found` | Count | A reservation is refused |
| `Quota.RESERVATION_SPANNED_GRANTS` | `quota.reservation.spanned_grants` | Count | A reservation draws on more than one grant |
| `Quota.SETTLED_QUANTITY` | `quota.settlement.quantity` | Value | A settlement succeeds |
| `Quota.GRANT_OVERDRAWN` | `quota.settlement.grant_overdrawn` | Count | A settlement overdraws |
| `Quota.GRANTS_RUNNING_LOW` | `quota.settlement.grants_running_low` | Count | Less than 10% remains after a settlement |
| `Grants.GRANT_SAVED` | `entitlements.grant.saved` | Count | A grant is created |
| `Grants.GRANT_UPDATED` | `entitlements.grant.updated` | Count | A grant is updated |
| `Grants.GRANT_DELETED` | `entitlements.grant.deleted` | Count | A grant is deleted |
| `Grants.GRANT_QUANTITY` | `entitlements.grant.quantity` | Value | A grant is created or updated |
| `Grants.GRANT_VALIDITY_DAYS` | `entitlements.grant.validity_days` | Value | A grant is created |
| `Grants.GRANT_ACTIVE_COUNT` | `entitlements.grant.active_count` | Value | Live grants are loaded |
| `Grants.GRANT_TOTAL_COUNT` | `entitlements.grant.total_count` | Value | Grants are listed |
| `Consumption.RESERVATION_TAKEN` | `metering.reservation.taken` | Count | A hold is written |
| `Consumption.RESERVATION_QUANTITY` | `metering.reservation.quantity` | Value | A hold is written |
| `Consumption.RESERVATION_SETTLED` | `metering.reservation.settled` | Count | Holds are settled |
| `Consumption.CONSUMPTION_QUANTITY` | `metering.consumption.quantity` | Value | Holds are settled |
| `Consumption.RESERVATION_RELEASED` | `metering.reservation.released` | Count | Holds are released |
| `Consumption.RESERVATION_ABANDONED` | `metering.reservation.abandoned` | Value | Abandoned holds are counted, zero included |
| `Consumption.CONSUMPTION_TOTAL_QUERIED` | `metering.consumption.total_queried` | Value | A total is read |
| `Consumption.CONSUMPTION_GRANTS_QUERIED` | `metering.consumption.grants_queried` | Value | Grant balances are read |
| `Consumption.CONSUMPTION_TIMESERIES_QUERIED` | `metering.consumption.timeseries_queried` | Value | A time series is read |
| `Consumption.CONSUMPTION_EVENTS_LISTED` | `metering.consumption.events_listed` | Value | Events are listed |

## Notes

- Constants live on `BillingMetricNames`
- `GRANT_OVERDRAWN` counts work delivered that no grant had room for — the evidence for estimating work more accurately up front
- Logging goes through `ILogger<T>`; telemetry is metrics only
