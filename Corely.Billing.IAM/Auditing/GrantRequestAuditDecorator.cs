using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Audits.Models;
using Corely.IAM.Audits.Providers;
using Corely.IAM.Security.Enums;

namespace Corely.Billing.IAM.Auditing;

internal sealed class GrantRequestAuditDecorator(
    IGrantRequestService inner,
    IAuditProvider auditProvider
) : IGrantRequestService
{
    private const string SERVICE = nameof(IGrantRequestService);

    public Task<RequestGrantResult> RequestGrantAsync(
        RequestGrantRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrantRequest(AuthAction.Create, request.AccountId),
            () => inner.RequestGrantAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode, r.CreatedId)
        );

    public Task<RetrieveSingleResult<GrantRequest>> GetGrantRequestAsync(
        Guid accountId,
        Guid grantRequestId,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrantRequest(AuthAction.Read, accountId, grantRequestId),
            () => inner.GetGrantRequestAsync(accountId, grantRequestId, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<RetrieveListResult<GrantRequest>> ListGrantRequestsAsync(
        ListGrantRequestsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrantRequest(AuthAction.Read, request.AccountId),
            () => inner.ListGrantRequestsAsync(request, authorizedResourceIds, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<ApproveGrantRequestResult> ApproveGrantRequestAsync(
        ApproveGrantRequestRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrantRequest(AuthAction.Update, request.AccountId, request.GrantRequestId),
            () => inner.ApproveGrantRequestAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<DeclineGrantRequestResult> DeclineGrantRequestAsync(
        DeclineGrantRequestRequest request,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnGrantRequest(AuthAction.Update, request.AccountId, request.GrantRequestId),
            () => inner.DeclineGrantRequestAsync(request, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    private static AuditCall OnGrantRequest(
        AuthAction action,
        Guid accountId,
        params Guid[] grantRequestIds
    ) =>
        new(SERVICE, action, BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE)
        {
            AccountId = accountId,
            ResourceIds = grantRequestIds,
        };
}
