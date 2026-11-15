using Corely.Billing.Extensions;
using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Usage;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Services;

internal class UsagePriceServiceTelemetryDecorator(
    IUsagePriceService inner,
    ILogger<UsagePriceServiceTelemetryDecorator> logger
) : IUsagePriceService
{
    private readonly IUsagePriceService _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<UsagePriceServiceTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );

    public PriceSource Source => _inner.Source;

    public Task<Money?> GetPriceAsync(UsageOperation operation, CancellationToken ct = default) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(UsagePriceService),
            operation,
            () => _inner.GetPriceAsync(operation, ct)
        );

    public Task<IReadOnlyList<UsagePrice>> ListPricesAsync(CancellationToken ct = default) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(UsagePriceService),
            Source,
            () => _inner.ListPricesAsync(ct)
        );

    public Task<ChangeUsagePriceResult> SetPriceAsync(
        UsagePrice price,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(UsagePriceService),
            price,
            () => _inner.SetPriceAsync(price, ct),
            logResult: true
        );

    public Task<ChangeUsagePriceResult> ClearPriceAsync(
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(UsagePriceService),
            operation,
            () => _inner.ClearPriceAsync(operation, ct),
            logResult: true
        );
}
