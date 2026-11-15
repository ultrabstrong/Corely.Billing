using Corely.Billing.Accounts.Models;

namespace Corely.Billing.Accounts.Processors;

internal interface IAccountDataProcessor
{
    Task<DeleteAccountDataResult> DeleteAccountDataAsync(
        Guid accountId,
        CancellationToken ct = default
    );
}
