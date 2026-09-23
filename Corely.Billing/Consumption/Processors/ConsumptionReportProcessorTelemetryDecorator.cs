using Corely.Billing.Consumption.Models;
using Corely.Billing.Extensions;
using Corely.Billing.Models;
using Corely.Billing.Telemetry;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Consumption.Processors;

internal class ConsumptionReportProcessorTelemetryDecorator(
    IConsumptionReportProcessor inner,
    ILogger<ConsumptionReportProcessorTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IConsumptionReportProcessor
{
    private readonly IConsumptionReportProcessor _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<ConsumptionReportProcessorTelemetryDecorator> _logger =
        logger.ThrowIfNull(nameof(logger));
    private readonly IBillingTelemetry _telemetry = telemetry.ThrowIfNull(nameof(telemetry));

    public async Task<long> GetConsumptionTotalAsync(
        GetConsumptionTotalRequest request,
        CancellationToken ct = default
    )
    {
        var total = await _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReportProcessor),
            request,
            () => _inner.GetConsumptionTotalAsync(request, ct)
        );
        _telemetry.Record(BillingMetricNames.Consumption.CONSUMPTION_TOTAL_QUERIED, total);
        return total;
    }

    public async Task<List<GrantTotalConsumptions>> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        IReadOnlyList<Guid> grantIds,
        CancellationToken ct = default
    )
    {
        var totals = await _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReportProcessor),
            accountId,
            () => _inner.GetGrantConsumptionTotalsAsync(accountId, grantIds, ct)
        );
        _telemetry.Record(
            BillingMetricNames.Consumption.CONSUMPTION_GRANTS_QUERIED,
            grantIds.Count
        );
        return totals;
    }

    public async Task<List<ConsumptionTimeBucketData>> GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    )
    {
        var buckets = await _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReportProcessor),
            request,
            () => _inner.GetConsumptionTimeSeriesAsync(request, ct)
        );
        _telemetry.Record(
            BillingMetricNames.Consumption.CONSUMPTION_TIMESERIES_QUERIED,
            buckets.Count
        );
        return buckets;
    }

    public async Task<PagedResult<ConsumptionEvent>> ListConsumptionEventsAsync(
        ListConsumptionEventsRequest request,
        CancellationToken ct = default
    )
    {
        var page = await _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReportProcessor),
            request,
            () => _inner.ListConsumptionEventsAsync(request, ct)
        );
        _telemetry.Record(
            BillingMetricNames.Consumption.CONSUMPTION_EVENTS_LISTED,
            page.TotalCount
        );
        return page;
    }

    public Task<List<string>> ListProvidersAsync(Guid accountId, CancellationToken ct = default) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReportProcessor),
            accountId,
            () => _inner.ListProvidersAsync(accountId, ct)
        );

    public Task<DateTime?> GetEarliestConsumptionAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReportProcessor),
            accountId,
            () => _inner.GetEarliestConsumptionAsync(accountId, ct)
        );

    public async Task<int> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var count = await _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReportProcessor),
            accountId,
            () => _inner.CountAbandonedReservationsAsync(accountId, ct)
        );

        _telemetry.Record(BillingMetricNames.Consumption.RESERVATION_ABANDONED, count);
        return count;
    }
}
