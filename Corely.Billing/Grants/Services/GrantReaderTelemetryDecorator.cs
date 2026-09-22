using Corely.Billing;
using Corely.Billing.Grants.Models;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Grants.Services;

internal sealed class GrantReaderTelemetryDecorator(
    IGrantReader inner,
    ILogger<GrantReaderTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IGrantReader
{
    public async Task<GetAllGrantsResult> GetAllGrantsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(GrantReader),
            accountId,
            () => inner.GetAllGrantsAsync(accountId, ct)
        );
        if (result.ResultCode == GetAllGrantsResultCode.Success)
            telemetry.Record(
                BillingMetricNames.Entitlements.GRANT_TOTAL_COUNT,
                result.Grants?.Count ?? 0
            );
        return result;
    }

    public Task<GetGrantByIdResult> GetGrantByIdAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        logger.ExecuteWithLoggingAsync(
            nameof(GrantReader),
            grantId,
            () => inner.GetGrantByIdAsync(accountId, grantId, ct)
        );

    public async Task<GetActiveGrantsResult> GetActiveGrantsAsync(
        Guid accountId,
        DateTime atUtc,
        UsageOperation? operation = null,
        UsageUnit? unit = null,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(GrantReader),
            accountId,
            () => inner.GetActiveGrantsAsync(accountId, atUtc, operation, unit, tags, ct)
        );
        if (result.ResultCode == GetActiveGrantsResultCode.Success)
            telemetry.Record(
                BillingMetricNames.Entitlements.GRANT_ACTIVE_COUNT,
                result.Grants?.Count ?? 0
            );
        return result;
    }
}
