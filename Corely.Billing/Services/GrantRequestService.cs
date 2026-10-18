using Corely.Billing.GrantRequests.Models;
using Corely.Billing.GrantRequests.Processors;
using Corely.Billing.Models;
using Corely.Common.Extensions;

namespace Corely.Billing.Services;

internal class GrantRequestService(IGrantRequestProcessor grantRequestProcessor)
    : IGrantRequestService
{
    private readonly IGrantRequestProcessor _grantRequestProcessor =
        grantRequestProcessor.ThrowIfNull(nameof(grantRequestProcessor));

    public Task<RequestGrantResult> RequestGrantAsync(
        RequestGrantRequest request,
        CancellationToken ct = default
    ) => _grantRequestProcessor.RequestGrantAsync(request, ct);

    public Task<RetrieveSingleResult<GrantRequest>> GetGrantRequestAsync(
        Guid accountId,
        Guid grantRequestId,
        CancellationToken ct = default
    ) => _grantRequestProcessor.GetGrantRequestAsync(accountId, grantRequestId, ct);

    public Task<RetrieveListResult<GrantRequest>> ListGrantRequestsAsync(
        ListGrantRequestsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    ) => _grantRequestProcessor.ListGrantRequestsAsync(request, authorizedResourceIds, ct);

    public Task<ApproveGrantRequestResult> ApproveGrantRequestAsync(
        ApproveGrantRequestRequest request,
        CancellationToken ct = default
    ) => _grantRequestProcessor.ApproveGrantRequestAsync(request, ct);

    public Task<DeclineGrantRequestResult> DeclineGrantRequestAsync(
        DeclineGrantRequestRequest request,
        CancellationToken ct = default
    ) => _grantRequestProcessor.DeclineGrantRequestAsync(request, ct);
}
