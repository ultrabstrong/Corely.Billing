using Corely.Billing.GrantRequests.Models;
using Corely.Billing.IAM.Extensions;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Providers;

namespace Corely.Billing.IAM.Authorization;

internal sealed class GrantRequestAuthorizationDecorator(
    IGrantRequestService inner,
    IAuthorizationProvider authorizationProvider,
    IUserContextProvider userContextProvider,
    AccountReadScope accountReadScope
) : IGrantRequestService
{
    private Guid? ActingUserId(Guid? claimed) =>
        userContextProvider.GetUserContext().ActingUserId(claimed);

    public async Task<RequestGrantResult> RequestGrantAsync(
        RequestGrantRequest request,
        CancellationToken ct = default
    ) =>
        authorizationProvider.HasAccountContext(request.AccountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Create,
            BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE
        )
            ? await inner.RequestGrantAsync(
                request with
                {
                    RequestedByUserId = ActingUserId(request.RequestedByUserId),
                },
                ct
            )
            : new RequestGrantResult(
                RequestGrantResultCode.UnauthorizedError,
                "Unauthorized to request a grant",
                Guid.Empty
            );

    public async Task<RetrieveSingleResult<GrantRequest>> GetGrantRequestAsync(
        Guid accountId,
        Guid grantRequestId,
        CancellationToken ct = default
    ) =>
        authorizationProvider.HasAccountContext(accountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Read,
            BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE,
            grantRequestId
        )
            ? await inner.GetGrantRequestAsync(accountId, grantRequestId, ct)
            : new RetrieveSingleResult<GrantRequest>(
                RetrieveResultCode.UnauthorizedError,
                $"Unauthorized to read grant request {grantRequestId}",
                null
            );

    public async Task<RetrieveListResult<GrantRequest>> ListGrantRequestsAsync(
        ListGrantRequestsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    )
    {
        if (
            !await accountReadScope.CanReadAsync(
                request.AccountIds,
                BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE
            )
        )
        {
            return new RetrieveListResult<GrantRequest>(
                RetrieveResultCode.UnauthorizedError,
                "Unauthorized to list grant requests",
                null
            );
        }

        return await inner.ListGrantRequestsAsync(
            request,
            await authorizationProvider.GetAuthorizedResourceIdsAsync(
                AuthAction.Read,
                BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE
            ),
            ct
        );
    }

    public async Task<ApproveGrantRequestResult> ApproveGrantRequestAsync(
        ApproveGrantRequestRequest request,
        CancellationToken ct = default
    ) =>
        authorizationProvider.HasAccountContext(request.AccountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Update,
            BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE,
            request.GrantRequestId
        )
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Create,
            BillingResourceTypes.GRANT_RESOURCE_TYPE
        )
            ? await inner.ApproveGrantRequestAsync(
                request with
                {
                    DecidedByUserId = ActingUserId(request.DecidedByUserId),
                },
                ct
            )
            : new ApproveGrantRequestResult(
                DecideGrantRequestResultCode.UnauthorizedError,
                $"Unauthorized to approve grant request {request.GrantRequestId}",
                Guid.Empty
            );

    public async Task<DeclineGrantRequestResult> DeclineGrantRequestAsync(
        DeclineGrantRequestRequest request,
        CancellationToken ct = default
    ) =>
        authorizationProvider.HasAccountContext(request.AccountId)
        && await authorizationProvider.IsAuthorizedAsync(
            AuthAction.Update,
            BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE,
            request.GrantRequestId
        )
            ? await inner.DeclineGrantRequestAsync(
                request with
                {
                    DecidedByUserId = ActingUserId(request.DecidedByUserId),
                },
                ct
            )
            : new DeclineGrantRequestResult(
                DecideGrantRequestResultCode.UnauthorizedError,
                $"Unauthorized to decline grant request {request.GrantRequestId}"
            );
}
