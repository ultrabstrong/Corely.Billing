using Corely.Billing.Accounts.Models;

namespace Corely.Billing.Services;

public interface IAccountDataService
{
    Task<DeleteAccountDataResult> DeleteAccountDataAsync(
        Guid accountId,
        CancellationToken ct = default
    );
}
