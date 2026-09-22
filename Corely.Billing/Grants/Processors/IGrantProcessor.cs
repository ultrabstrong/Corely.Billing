using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Grants.Processors;

internal interface IGrantProcessor
{
    Task<CreateGrantResult> CreateGrantAsync(
        CreateGrantRequest request,
        CancellationToken ct = default
    );

    Task<ModifyResult> UpdateGrantAsync(UpdateGrantRequest request, CancellationToken ct = default);

    Task<DeleteGrantResult> DeleteGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    );

    Task<RetrieveSingleResult<Grant>> GetGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    );

    Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        CancellationToken ct = default
    );

    Task<List<Grant>> ListActiveGrantsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        DateTime atUtc,
        CancellationToken ct = default
    );
}
