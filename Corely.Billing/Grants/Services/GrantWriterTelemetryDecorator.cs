using Corely.Billing;
using Corely.Billing.Grants.Models;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Grants.Services;

internal sealed class GrantWriterTelemetryDecorator(
    IGrantWriter inner,
    ILogger<GrantWriterTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IGrantWriter
{
    public async Task<SaveGrantResult> SaveAsync(Grant grant, CancellationToken ct = default)
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(GrantWriter),
            grant.GrantId,
            () => inner.SaveAsync(grant, ct)
        );
        if (result.ResultCode == SaveGrantResultCode.Success)
        {
            telemetry.Increment(BillingMetricNames.Entitlements.GRANT_SAVED);
            telemetry.Record(BillingMetricNames.Entitlements.GRANT_QUANTITY, grant.Quantity);
            telemetry.Record(
                BillingMetricNames.Entitlements.GRANT_VALIDITY_DAYS,
                (grant.ValidToUtc - grant.ValidFromUtc).TotalDays
            );
        }
        return result;
    }

    public async Task<UpdateGrantResult> UpdateAsync(Grant grant, CancellationToken ct = default)
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(GrantWriter),
            grant.GrantId,
            () => inner.UpdateAsync(grant, ct)
        );
        if (result.ResultCode == UpdateGrantResultCode.Success)
        {
            telemetry.Increment(BillingMetricNames.Entitlements.GRANT_UPDATED);
            telemetry.Record(BillingMetricNames.Entitlements.GRANT_QUANTITY, grant.Quantity);
        }
        return result;
    }

    public async Task<DeleteGrantResult> DeleteAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    )
    {
        var result = await logger.ExecuteWithLoggingAsync(
            nameof(GrantWriter),
            grantId,
            () => inner.DeleteAsync(accountId, grantId, ct)
        );
        if (result.ResultCode == DeleteGrantResultCode.Success)
            telemetry.Increment(BillingMetricNames.Entitlements.GRANT_DELETED);
        return result;
    }
}
