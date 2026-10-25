using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Providers;

namespace Corely.Billing.IAM.Authorization;

internal sealed class AccountReadScope(
    IAuthorizationProvider authorizationProvider,
    IUserContextProvider userContextProvider
)
{
    public async Task<bool> CanReadAsync(IReadOnlyList<Guid> accountIds, string resourceType)
    {
        if (accountIds.Count == 0)
            return false;

        if (accountIds.Count == 1 && authorizationProvider.HasAccountContext(accountIds[0]))
            return await authorizationProvider.IsAuthorizedAsync(AuthAction.Read, resourceType);

        return userContextProvider.GetUserContext() is { } context
            && (
                context.IsSystemContext
                || (
                    context.CurrentAccount?.IsPlatformAccount == true
                    && await authorizationProvider.IsAuthorizedAsync(
                        AuthAction.Read,
                        BillingResourceTypes.ALL_ACCOUNTS_RESOURCE_TYPE
                    )
                    && await authorizationProvider.IsAuthorizedAsync(AuthAction.Read, resourceType)
                )
            );
    }
}
