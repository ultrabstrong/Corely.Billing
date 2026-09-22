using Corely.Billing.Extensions;
using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Services;

internal class QuotaServiceTelemetryDecorator(
    IQuotaService inner,
    ILogger<QuotaServiceTelemetryDecorator> logger
) : IQuotaService
{
    private readonly IQuotaService _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<QuotaServiceTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );

    public Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(QuotaService),
            accountId,
            () => _inner.GetAvailabilityAsync(accountId, operation, unit, ct),
            logResult: true
        );

    public Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(QuotaService),
            request,
            () => _inner.ReserveAsync(request, ct),
            logResult: true
        );

    public Task<SettleQuotaResult> SettleAsync(
        SettleQuotaRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(QuotaService),
            request,
            () => _inner.SettleAsync(request, ct),
            logResult: true
        );

    public Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(QuotaService),
            request,
            () => _inner.ReleaseAsync(request, ct),
            logResult: true
        );
}
