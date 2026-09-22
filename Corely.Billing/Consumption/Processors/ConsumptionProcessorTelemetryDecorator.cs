using Corely.Billing.Consumption.Models;
using Corely.Billing.Extensions;
using Corely.Billing.Telemetry;
using Corely.Billing.Usage;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Consumption.Processors;

internal class ConsumptionProcessorTelemetryDecorator(
    IConsumptionProcessor inner,
    ILogger<ConsumptionProcessorTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IConsumptionProcessor
{
    private readonly IConsumptionProcessor _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<ConsumptionProcessorTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );
    private readonly IBillingTelemetry _telemetry = telemetry.ThrowIfNull(nameof(telemetry));

    public async Task<ReserveConsumptionResult> ReserveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionProcessor),
            consumptionEvent,
            () => _inner.ReserveAsync(consumptionEvent, ct),
            logResult: true
        );
        if (result.ResultCode == ReserveConsumptionResultCode.Success)
        {
            _telemetry.Increment(BillingMetricNames.Consumption.RESERVATION_TAKEN);
            _telemetry.Record(
                BillingMetricNames.Consumption.RESERVATION_QUANTITY,
                consumptionEvent.Quantity
            );
        }
        return result;
    }

    public async Task<ResolveConsumptionResult> SettleAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        IReadOnlyDictionary<Guid, long> quantityByGrant,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionProcessor),
            accountId,
            () => _inner.SettleAsync(accountId, operation, unit, quantityByGrant, ct),
            logResult: true
        );
        if (result.ResultCode == ResolveConsumptionResultCode.Success)
        {
            _telemetry.Increment(BillingMetricNames.Consumption.RESERVATION_SETTLED);
            _telemetry.Record(
                BillingMetricNames.Consumption.CONSUMPTION_QUANTITY,
                result.SettledQuantity
            );
        }
        return result;
    }

    public async Task<ResolveConsumptionResult> ReleaseAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionProcessor),
            accountId,
            () => _inner.ReleaseAsync(accountId, operation, unit, ct),
            logResult: true
        );
        if (result.ResultCode == ResolveConsumptionResultCode.Success)
            _telemetry.Increment(BillingMetricNames.Consumption.RESERVATION_RELEASED);
        return result;
    }

    public Task<List<ConsumptionEvent>?> ListOutstandingReservationsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionProcessor),
            accountId,
            () => _inner.ListOutstandingReservationsAsync(accountId, operation, unit, ct)
        );
}
