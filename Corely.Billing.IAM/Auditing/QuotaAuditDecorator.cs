using Corely.Billing.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Models;
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

    public Task<GetBalanceResult> GetBalanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Read, accountId),
            () => inner.GetBalanceAsync(accountId, operation, ct),
            r => AuditOutcome.Of(r.ResultCode)
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

    public Task<GetAllowanceResult> GetAllowanceAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Read, accountId),
            () => inner.GetAllowanceAsync(accountId, operation, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<HasFeatureResult> HasFeatureAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Read, accountId),
            () => inner.HasFeatureAsync(accountId, operation, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<GetCapacityResult> GetCapacityAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Read, accountId),
            () => inner.GetCapacityAsync(accountId, operation, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Execute, request.AccountId),
            () => inner.RegisterAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode, r.Registration?.RegistrationId ?? Guid.Empty)
        );

    public Task<ModifyResult> ReleaseRegistrationAsync(
        Guid accountId,
        Guid registrationId,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Execute, accountId) with
            {
                ResourceIds = [registrationId],
            },
            () => inner.ReleaseRegistrationAsync(accountId, registrationId, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<MoveRegistrationsResult> MoveRegistrationsAsync(
        MoveRegistrationsRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnQuota(AuthAction.Execute, request.AccountId) with
            {
                ResourceIds = [request.FromGrantId, request.ToGrantId],
            },
            () => inner.MoveRegistrationsAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<RetrieveListResult<Registration>> ListRegistrationsAsync(
        ListRegistrationsRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            new AuditCall(SERVICE, AuthAction.Read, BillingResourceTypes.QUOTA_RESOURCE_TYPE)
            {
                AccountId = request.AccountIds.Count == 1 ? request.AccountIds[0] : null,
            },
            () => inner.ListRegistrationsAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    private static AuditCall OnQuota(AuthAction action, Guid accountId) =>
        new(SERVICE, action, BillingResourceTypes.QUOTA_RESOURCE_TYPE) { AccountId = accountId };
}
