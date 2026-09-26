using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Security.Constants;
using Corely.IAM.Security.Providers;

namespace Corely.Billing.IAM.Authorization;

internal sealed class GrantAuthorizationDecorator(
    IGrantService inner,
    IAuthorizationProvider authorizationProvider
) : IGrantService
{
    public async Task<CreateGrantResult> CreateGrantAsync(
        CreateGrantRequest request,
        CancellationToken ct = default
    ) =>
        authorizationProvider.HasAccountContext(request.AccountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Create,
            BillingResourceTypes.GRANT_RESOURCE_TYPE
        )
            ? await inner.CreateGrantAsync(request, ct)
            : new CreateGrantResult(
                CreateGrantResultCode.UnauthorizedError,
                "Unauthorized to create grant",
                Guid.Empty
            );

    public async Task<RetrieveSingleResult<Grant>> GetGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        authorizationProvider.HasAccountContext(accountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Read,
            BillingResourceTypes.GRANT_RESOURCE_TYPE,
            grantId
        )
            ? await inner.GetGrantAsync(accountId, grantId, ct)
            : new RetrieveSingleResult<Grant>(
                RetrieveResultCode.UnauthorizedError,
                $"Unauthorized to read grant {grantId}",
                null
            );

    public async Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    )
    {
        if (
            !authorizationProvider.HasAccountContext(request.AccountId)
            || !await authorizationProvider.IsAuthorizedAsync(
                AuthAction.Read,
                BillingResourceTypes.GRANT_RESOURCE_TYPE
            )
        )
        {
            return new RetrieveListResult<Grant>(
                RetrieveResultCode.UnauthorizedError,
                "Unauthorized to list grants",
                null
            );
        }

        return await inner.ListGrantsAsync(
            request,
            await authorizationProvider.GetAuthorizedResourceIdsAsync(
                AuthAction.Read,
                BillingResourceTypes.GRANT_RESOURCE_TYPE
            ),
            ct
        );
    }

    public async Task<ModifyResult> UpdateGrantAsync(
        UpdateGrantRequest request,
        CancellationToken ct = default
    ) =>
        authorizationProvider.HasAccountContext(request.AccountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Update,
            BillingResourceTypes.GRANT_RESOURCE_TYPE,
            request.GrantId
        )
            ? await inner.UpdateGrantAsync(request, ct)
            : new ModifyResult(
                ModifyResultCode.UnauthorizedError,
                $"Unauthorized to update grant {request.GrantId}"
            );

    public async Task<DeleteGrantResult> DeleteGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        authorizationProvider.HasAccountContext(accountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Delete,
            BillingResourceTypes.GRANT_RESOURCE_TYPE,
            grantId
        )
            ? await inner.DeleteGrantAsync(accountId, grantId, ct)
            : new DeleteGrantResult(
                DeleteGrantResultCode.UnauthorizedError,
                $"Unauthorized to delete grant {grantId}"
            );
}
