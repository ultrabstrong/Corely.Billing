using Corely.Billing.Accounts.Models;
using Corely.Billing.Extensions;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Accounts.Processors;

internal class AccountDataProcessorTelemetryDecorator(
    IAccountDataProcessor inner,
    ILogger<AccountDataProcessorTelemetryDecorator> logger
) : IAccountDataProcessor
{
    private readonly IAccountDataProcessor _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<AccountDataProcessorTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );

    public Task<DeleteAccountDataResult> DeleteAccountDataAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(AccountDataProcessor),
            accountId,
            () => _inner.DeleteAccountDataAsync(accountId, ct),
            logResult: true
        );
}
