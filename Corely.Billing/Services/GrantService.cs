using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Models;
using Corely.Common.Extensions;

namespace Corely.Billing.Services;

internal class GrantService(IGrantProcessor grantProcessor) : IGrantService
{
    private readonly IGrantProcessor _grantProcessor = grantProcessor.ThrowIfNull(
        nameof(grantProcessor)
    );

    public Task<CreateGrantResult> CreateGrantAsync(
        CreateGrantRequest request,
        CancellationToken ct = default
    ) => _grantProcessor.CreateGrantAsync(request, ct);

    public Task<RetrieveSingleResult<Grant>> GetGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) => _grantProcessor.GetGrantAsync(accountId, grantId, ct);

    public Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        CancellationToken ct = default
    ) => _grantProcessor.ListGrantsAsync(request, ct);

    public Task<ModifyResult> UpdateGrantAsync(
        UpdateGrantRequest request,
        CancellationToken ct = default
    ) => _grantProcessor.UpdateGrantAsync(request, ct);

    public Task<DeleteGrantResult> DeleteGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) => _grantProcessor.DeleteGrantAsync(accountId, grantId, ct);
}
