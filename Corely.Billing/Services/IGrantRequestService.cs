using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Models;

namespace Corely.Billing.Services;

public interface IGrantRequestService
{
    Task<RequestGrantResult> RequestGrantAsync(
        RequestGrantRequest request,
        CancellationToken ct = default
    );

    Task<RetrieveSingleResult<GrantRequest>> GetGrantRequestAsync(
        Guid accountId,
        Guid grantRequestId,
        CancellationToken ct = default
    );

    Task<RetrieveListResult<GrantRequest>> ListGrantRequestsAsync(
        ListGrantRequestsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    );

    Task<ApproveGrantRequestResult> ApproveGrantRequestAsync(
        ApproveGrantRequestRequest request,
        CancellationToken ct = default
    );

    Task<DeclineGrantRequestResult> DeclineGrantRequestAsync(
        DeclineGrantRequestRequest request,
        CancellationToken ct = default
    );
}
