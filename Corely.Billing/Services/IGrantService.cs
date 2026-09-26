using Corely.Billing.Grants.Models;
using Corely.Billing.Models;

namespace Corely.Billing.Services;

public interface IGrantService
{
    Task<CreateGrantResult> CreateGrantAsync(
        CreateGrantRequest request,
        CancellationToken ct = default
    );

    Task<RetrieveSingleResult<Grant>> GetGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    );

    Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    );

    Task<ModifyResult> UpdateGrantAsync(UpdateGrantRequest request, CancellationToken ct = default);

    Task<DeleteGrantResult> DeleteGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    );
}
