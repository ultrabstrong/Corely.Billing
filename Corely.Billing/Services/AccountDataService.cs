using Corely.Billing.Accounts.Models;
using Corely.Billing.Accounts.Processors;
using Corely.Common.Extensions;

namespace Corely.Billing.Services;

internal class AccountDataService(IAccountDataProcessor accountDataProcessor) : IAccountDataService
{
    private readonly IAccountDataProcessor _accountDataProcessor = accountDataProcessor.ThrowIfNull(
        nameof(accountDataProcessor)
    );

    public Task<DeleteAccountDataResult> DeleteAccountDataAsync(
        Guid accountId,
        CancellationToken ct = default
    ) => _accountDataProcessor.DeleteAccountDataAsync(accountId, ct);
}
