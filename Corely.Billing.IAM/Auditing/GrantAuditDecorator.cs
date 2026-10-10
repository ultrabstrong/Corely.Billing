using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Audits.Models;
using Corely.IAM.Audits.Providers;
using Corely.IAM.Security.Constants;

namespace Corely.Billing.IAM.Auditing;

internal sealed class GrantAuditDecorator(IGrantService inner, IAuditProvider auditProvider)
    : IGrantService
{
    private const string SERVICE = nameof(IGrantService);

    public Task<CreateGrantResult> CreateGrantAsync(
        CreateGrantRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrant(AuthAction.Create, request.AccountId),
            () => inner.CreateGrantAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode, r.CreatedId)
        );

    public Task<RetrieveSingleResult<Grant>> GetGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrant(AuthAction.Read, accountId, grantId),
            () => inner.GetGrantAsync(accountId, grantId, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrant(AuthAction.Read, request.AccountId),
            () => inner.ListGrantsAsync(request, authorizedResourceIds, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<ModifyResult> UpdateGrantAsync(
        UpdateGrantRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrant(AuthAction.Update, request.AccountId, request.GrantId),
            () => inner.UpdateGrantAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<DeleteGrantResult> DeleteGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrant(AuthAction.Delete, accountId, grantId),
            () => inner.DeleteGrantAsync(accountId, grantId, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    private static AuditCall OnGrant(AuthAction action, Guid accountId, params Guid[] grantIds) =>
        new(SERVICE, action, BillingResourceTypes.GRANT_RESOURCE_TYPE)
        {
            AccountId = accountId,
            ResourceIds = grantIds,
        };
}
