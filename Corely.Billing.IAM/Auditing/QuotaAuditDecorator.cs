using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.IAM.Audits.Models;
using Corely.IAM.Audits.Providers;
using Corely.IAM.Security.Enums;

namespace Corely.Billing.IAM.Auditing;

internal sealed class QuotaAuditDecorator(IQuotaService inner, IAuditProvider auditProvider)
    : IQuotaService
{
    private const string SERVICE = nameof(IQuotaService);

    public Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Read, accountId),
            () => inner.GetAvailabilityAsync(accountId, operation, unit, ct),
            availability => AuditOutcome.Of(availability)
        );

    public Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Execute, request.AccountId),
            () => inner.ReserveAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<SettleQuotaResult> SettleAsync(
        SettleQuotaRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Execute, request.AccountId),
            () => inner.SettleAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Execute, request.AccountId),
            () => inner.ReleaseAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    private static AuditCall OnQuota(AuthAction action, Guid accountId) =>
        new(SERVICE, action, BillingResourceTypes.QUOTA_RESOURCE_TYPE) { AccountId = accountId };
}
