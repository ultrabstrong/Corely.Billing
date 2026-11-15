using Corely.Billing.Accounts.Models;
using Corely.Billing.Extensions;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Services;

internal class AccountDataServiceTelemetryDecorator(
    IAccountDataService inner,
    ILogger<AccountDataServiceTelemetryDecorator> logger
) : IAccountDataService
{
    private readonly IAccountDataService _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<AccountDataServiceTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );

    public Task<DeleteAccountDataResult> DeleteAccountDataAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(AccountDataService),
            accountId,
            () => _inner.DeleteAccountDataAsync(accountId, ct),
            logResult: true
        );
}
