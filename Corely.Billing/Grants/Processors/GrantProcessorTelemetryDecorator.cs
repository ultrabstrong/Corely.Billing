using Corely.Billing.Extensions;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Telemetry;
using Corely.Billing.Usage;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Grants.Processors;

internal class GrantProcessorTelemetryDecorator(
    IGrantProcessor inner,
    ILogger<GrantProcessorTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IGrantProcessor
{
    private readonly IGrantProcessor _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<GrantProcessorTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );
    private readonly IBillingTelemetry _telemetry = telemetry.ThrowIfNull(nameof(telemetry));

    public async Task<CreateGrantResult> CreateGrantAsync(
        CreateGrantRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(GrantProcessor),
            request,
            () => _inner.CreateGrantAsync(request, ct),
            logResult: true
        );
        if (result.ResultCode == CreateGrantResultCode.Success)
        {
            _telemetry.Increment(BillingMetricNames.Grants.GRANT_SAVED);
            if (request.Quantity is { } quantity)
                _telemetry.Record(BillingMetricNames.Grants.GRANT_QUANTITY, quantity);
            _telemetry.Record(
                BillingMetricNames.Grants.GRANT_VALIDITY_DAYS,
                (request.ValidToUtc - request.ValidFromUtc).TotalDays
            );
        }
        return result;
    }

    public async Task<ModifyResult> UpdateGrantAsync(
        UpdateGrantRequest request,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(GrantProcessor),
            request,
            () => _inner.UpdateGrantAsync(request, ct),
            logResult: true
        );
        if (result.ResultCode == ModifyResultCode.Success)
        {
            _telemetry.Increment(BillingMetricNames.Grants.GRANT_UPDATED);
            if (request.Quantity is { } quantity)
                _telemetry.Record(BillingMetricNames.Grants.GRANT_QUANTITY, quantity);
        }
        return result;
    }

    public async Task<DeleteGrantResult> DeleteGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(GrantProcessor),
            grantId,
            () => _inner.DeleteGrantAsync(accountId, grantId, ct),
            logResult: true
        );
        if (result.ResultCode == DeleteGrantResultCode.Success)
            _telemetry.Increment(BillingMetricNames.Grants.GRANT_DELETED);
        return result;
    }

    public Task<RetrieveSingleResult<Grant>> GetGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(GrantProcessor),
            grantId,
            () => _inner.GetGrantAsync(accountId, grantId, ct)
        );

    public async Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(GrantProcessor),
            request,
            () => _inner.ListGrantsAsync(request, authorizedResourceIds, ct)
        );
        if (result.ResultCode == RetrieveResultCode.Success)
            _telemetry.Record(
                BillingMetricNames.Grants.GRANT_TOTAL_COUNT,
                result.Data?.TotalCount ?? 0
            );
        return result;
    }

    public async Task<List<Grant>> ListActiveGrantsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        DateTime atUtc,
        CancellationToken ct = default
    )
    {
        var grants = await _logger.ExecuteWithLoggingAsync(
            nameof(GrantProcessor),
            accountId,
            () => _inner.ListActiveGrantsAsync(accountId, operation, unit, atUtc, ct)
        );
        _telemetry.Record(BillingMetricNames.Grants.GRANT_ACTIVE_COUNT, grants.Count);
        return grants;
    }
}
