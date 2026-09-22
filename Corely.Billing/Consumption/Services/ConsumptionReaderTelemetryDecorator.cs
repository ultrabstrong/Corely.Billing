using Corely.Billing;
using Corely.Billing.Consumption.Models;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Consumption.Services;

internal sealed class ConsumptionReaderTelemetryDecorator(
    IConsumptionReader inner,
    ILogger<ConsumptionReaderTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IConsumptionReader
{
    public Task<GetOutstandingReservationsResult> GetOutstandingReservationsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) =>
        logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReader),
            accountId,
            () => inner.GetOutstandingReservationsAsync(accountId, operation, unit, ct)
        );

    public async Task<int> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var count = await logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReader),
            accountId,
            () => inner.CountAbandonedReservationsAsync(accountId, ct)
        );

        // Recorded even at zero, so the metric distinguishes "none abandoned" from "nobody asked".
        telemetry.Record(BillingMetricNames.Metering.RESERVATION_ABANDONED, count);
        return count;
    }

    public async Task<GetConsumptionTotalResult> GetConsumptionTotalAsync(
        Guid accountId,
        UsageUnit unit,
        UsageOperation operation,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? provider = null,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReader),
            accountId,
            () =>
                inner.GetConsumptionTotalAsync(
                    accountId,
                    unit,
                    operation,
                    fromUtc,
                    toUtc,
                    provider,
                    tags,
                    ct
                )
        );
        if (result.ResultCode == GetConsumptionTotalResultCode.Success)
            telemetry.Record(
                BillingMetricNames.Metering.CONSUMPTION_TOTAL_QUERIED,
                (double)result.Total
            );
        return result;
    }

    public async Task<GetGrantConsumptionTotalsResult> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        Guid[] grantIds,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReader),
            accountId,
            () => inner.GetGrantConsumptionTotalsAsync(accountId, grantIds, ct)
        );
        if (result.ResultCode == GetGrantConsumptionTotalsResultCode.Success)
            telemetry.Record(
                BillingMetricNames.Metering.CONSUMPTION_GRANTS_QUERIED,
                grantIds.Length
            );
        return result;
    }

    public async Task<GetConsumptionTimeSeriesResult> GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReader),
            request.AccountId,
            () => inner.GetConsumptionTimeSeriesAsync(request, ct)
        );
        if (result.ResultCode == GetConsumptionTimeSeriesResultCode.Success)
            telemetry.Record(
                BillingMetricNames.Metering.CONSUMPTION_TIMESERIES_QUERIED,
                result.Buckets?.Count ?? 0
            );
        return result;
    }

    public async Task<ListConsumptionEventsResult> ListConsumptionEventsAsync(
        Guid accountId,
        int skip,
        int take,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        IReadOnlyList<UsageUnit>? units = null,
        IReadOnlyList<UsageOperation>? operations = null,
        IReadOnlyList<string>? providers = null,
        IReadOnlyList<Guid>? grantIds = null,
        string? sortColumn = null,
        bool sortDescending = false,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionReader),
            accountId,
            () =>
                inner.ListConsumptionEventsAsync(
                    accountId,
                    skip,
                    take,
                    fromUtc,
                    toUtc,
                    units,
                    operations,
                    providers,
                    grantIds,
                    sortColumn,
                    sortDescending,
                    ct
                )
        );
        if (result.ResultCode == ListConsumptionEventsResultCode.Success)
            telemetry.Record(
                BillingMetricNames.Metering.CONSUMPTION_EVENTS_LISTED,
                result.TotalCount
            );
        return result;
    }

    public Task<List<string>> GetDistinctProvidersAsync(
        Guid accountId,
        CancellationToken ct = default
    ) => inner.GetDistinctProvidersAsync(accountId, ct);

    public Task<DateTime?> GetEarliestEventDateAsync(
        Guid accountId,
        CancellationToken ct = default
    ) => inner.GetEarliestEventDateAsync(accountId, ct);
}
