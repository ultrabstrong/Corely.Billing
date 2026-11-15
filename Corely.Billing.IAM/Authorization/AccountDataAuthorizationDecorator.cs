using Corely.Billing.Accounts.Models;
using Corely.Billing.Services;
using Corely.IAM.Users.Providers;

namespace Corely.Billing.IAM.Authorization;

internal sealed class AccountDataAuthorizationDecorator(
    IAccountDataService inner,
    IUserContextProvider userContextProvider
) : IAccountDataService
{
    public async Task<DeleteAccountDataResult> DeleteAccountDataAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        userContextProvider.GetUserContext() is { IsSystemContext: true }
            ? await inner.DeleteAccountDataAsync(accountId, ct)
            : new DeleteAccountDataResult(
                DeleteAccountDataResultCode.UnauthorizedError,
                "Only the system deletes an account's billing data, when the account is deleted"
            );
}
