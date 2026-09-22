using Corely.Billing;
using Corely.Billing.Quota.Models;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Quota.Services;

internal sealed class QuotaTelemetryDecorator(
    IQuotaService inner,
    ILogger<QuotaTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IQuotaService
{
    private const double RUNNING_LOW_RATIO = 0.10;

    public Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) => inner.GetAvailabilityAsync(accountId, operation, unit, ct);

    public async Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(QuotaService),
            request.AccountId,
            () => inner.ReserveAsync(request, ct)
        );

        if (result.ResultCode == ReserveQuotaResultCode.Success)
        {
            telemetry.Increment(BillingMetricNames.Quota.GRANT_FOUND);

            // A reservation covering more than one grant means a document larger than the grant it
            // started on. Counted because the rate is what says whether grant sizes and document
            // sizes are mismatched.
            if (result.Shares is { Count: > 1 })
                telemetry.Increment(BillingMetricNames.Quota.RESERVATION_SPANNED_GRANTS);
        }
        else
        {
            telemetry.Increment(BillingMetricNames.Quota.GRANT_NOT_FOUND);
        }

        return result;
    }

    public async Task<SettleQuotaResult> SettleAsync(
        SettleQuotaRequest request,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(QuotaService),
            request.AccountId,
            () => inner.SettleAsync(request, ct)
        );

        if (result.ResultCode == SettleQuotaResultCode.Success)
        {
            telemetry.Record(BillingMetricNames.Quota.SETTLED_QUANTITY, result.SettledQuantity);

            // Every overdraft is work delivered that no grant had room for. The count per period is
            // the evidence that a pre-flight page count is worth building for whatever format keeps
            // producing them.
            if (result.Overdrawn)
                telemetry.Increment(BillingMetricNames.Quota.GRANT_OVERDRAWN);

            if (result.RemainingRatio < RUNNING_LOW_RATIO)
                telemetry.Increment(BillingMetricNames.Quota.GRANTS_RUNNING_LOW);
        }

        return result;
    }

    public Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    ) =>
        logger.ExecuteWithLoggingAsync(
            nameof(QuotaService),
            request.AccountId,
            () => inner.ReleaseAsync(request, ct)
        );
}
