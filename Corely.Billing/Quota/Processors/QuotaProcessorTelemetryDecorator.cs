using Corely.Billing.Extensions;
using Corely.Billing.Quota.Models;
using Corely.Billing.Telemetry;
using Corely.Billing.Usage;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Quota.Processors;

internal class QuotaProcessorTelemetryDecorator(
    IQuotaProcessor inner,
    ILogger<QuotaProcessorTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IQuotaProcessor
{
    private const double RUNNING_LOW_RATIO = 0.10;

    private readonly IQuotaProcessor _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<QuotaProcessorTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );
    private readonly IBillingTelemetry _telemetry = telemetry.ThrowIfNull(nameof(telemetry));

    public Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(QuotaProcessor),
            accountId,
            () => _inner.GetAvailabilityAsync(accountId, operation, unit, ct),
            logResult: true
        );

    public async Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(QuotaProcessor),
            request,
            () => _inner.ReserveAsync(request, ct),
            logResult: true
        );
        if (result.ResultCode == ReserveQuotaResultCode.Success)
        {
            _telemetry.Increment(BillingMetricNames.Quota.GRANT_FOUND);

            // A reservation covering more than one grant means work larger than the grant it started
            // on. Counted because the rate is what says whether grant sizes and work sizes are
            // mismatched.
            if (result.Shares is { Count: > 1 })
                _telemetry.Increment(BillingMetricNames.Quota.RESERVATION_SPANNED_GRANTS);
        }
        else
        {
            _telemetry.Increment(BillingMetricNames.Quota.GRANT_NOT_FOUND);
        }
        return result;
    }

    public async Task<SettleQuotaResult> SettleAsync(
        SettleQuotaRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(QuotaProcessor),
            request,
            () => _inner.SettleAsync(request, ct),
            logResult: true
        );
        if (result.ResultCode == SettleQuotaResultCode.Success)
        {
            _telemetry.Record(BillingMetricNames.Quota.SETTLED_QUANTITY, result.SettledQuantity);

            // Every overdraft is work delivered that no grant had room for. The count per period is
            // the evidence for estimating work more accurately before it starts.
            if (result.Overdrawn)
                _telemetry.Increment(BillingMetricNames.Quota.GRANT_OVERDRAWN);
            if (result.RemainingRatio < RUNNING_LOW_RATIO)
                _telemetry.Increment(BillingMetricNames.Quota.GRANTS_RUNNING_LOW);
        }
        return result;
    }

    public Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(QuotaProcessor),
            request,
            () => _inner.ReleaseAsync(request, ct),
            logResult: true
        );
}
