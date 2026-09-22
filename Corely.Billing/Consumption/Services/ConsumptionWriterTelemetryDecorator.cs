using Corely.Billing;
using Corely.Billing.Consumption.Models;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Consumption.Services;

internal sealed class ConsumptionWriterTelemetryDecorator(
    IConsumptionWriter inner,
    ILogger<ConsumptionWriterTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IConsumptionWriter
{
    public async Task<SaveConsumptionResult> SaveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionWriter),
            consumptionEvent.AccountId,
            () => inner.SaveAsync(consumptionEvent, ct)
        );
        if (result.ResultCode == SaveConsumptionResultCode.Success)
        {
            telemetry.Increment(BillingMetricNames.Metering.CONSUMPTION_SAVED);
            telemetry.Record(
                BillingMetricNames.Metering.CONSUMPTION_QUANTITY,
                consumptionEvent.Quantity
            );
        }
        return result;
    }

    public async Task<SaveConsumptionResult> ReserveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionWriter),
            consumptionEvent.AccountId,
            () => inner.ReserveAsync(consumptionEvent, ct)
        );
        if (result.ResultCode == SaveConsumptionResultCode.Success)
        {
            telemetry.Increment(BillingMetricNames.Metering.RESERVATION_TAKEN);
            telemetry.Record(
                BillingMetricNames.Metering.RESERVATION_QUANTITY,
                consumptionEvent.Quantity
            );
        }
        return result;
    }

    public async Task<SettleConsumptionResult> SettleAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        IReadOnlyDictionary<Guid, long> quantityByGrant,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionWriter),
            accountId,
            () => inner.SettleAsync(accountId, operation, unit, quantityByGrant, ct)
        );
        if (result.ResultCode == SettleConsumptionResultCode.Success)
        {
            telemetry.Increment(BillingMetricNames.Metering.RESERVATION_SETTLED);
            telemetry.Record(
                BillingMetricNames.Metering.CONSUMPTION_QUANTITY,
                result.SettledQuantity
            );
        }
        return result;
    }

    public async Task<SettleConsumptionResult> ReleaseAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(ConsumptionWriter),
            accountId,
            () => inner.ReleaseAsync(accountId, operation, unit, ct)
        );
        if (result.ResultCode == SettleConsumptionResultCode.Success)
        {
            telemetry.Increment(BillingMetricNames.Metering.RESERVATION_RELEASED);
        }
        return result;
    }
}
