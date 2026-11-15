using Corely.Billing.Extensions;
using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Telemetry;
using Corely.Billing.Usage;
using Corely.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Prices.Processors;

internal class UsagePriceProcessorTelemetryDecorator(
    IUsagePriceProcessor inner,
    ILogger<UsagePriceProcessorTelemetryDecorator> logger,
    IBillingTelemetry telemetry
) : IUsagePriceProcessor
{
    private readonly IUsagePriceProcessor _inner = inner.ThrowIfNull(nameof(inner));
    private readonly ILogger<UsagePriceProcessorTelemetryDecorator> _logger = logger.ThrowIfNull(
        nameof(logger)
    );
    private readonly IBillingTelemetry _telemetry = telemetry.ThrowIfNull(nameof(telemetry));

    public PriceSource Source => _inner.Source;

    public Task<Money?> GetPriceAsync(UsageOperation operation, CancellationToken ct = default) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(UsagePriceProcessor),
            operation,
            () => _inner.GetPriceAsync(operation, ct)
        );

    public Task<IReadOnlyList<UsagePrice>> ListPricesAsync(CancellationToken ct = default) =>
        _logger.ExecuteWithLoggingAsync(
            nameof(UsagePriceProcessor),
            Source,
            () => _inner.ListPricesAsync(ct)
        );

    public async Task<ChangeUsagePriceResult> SetPriceAsync(
        UsagePrice price,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(UsagePriceProcessor),
            price,
            () => _inner.SetPriceAsync(price, ct),
            logResult: true
        );
        if (result.ResultCode == ChangeUsagePriceResultCode.Success)
            _telemetry.Increment(BillingMetricNames.Prices.PRICE_SET);
        return result;
    }

    public async Task<ChangeUsagePriceResult> ClearPriceAsync(
        UsageOperation operation,
        CancellationToken ct = default
    )
    {
        var result = await _logger.ExecuteWithLoggingAsync(
            nameof(UsagePriceProcessor),
            operation,
            () => _inner.ClearPriceAsync(operation, ct),
            logResult: true
        );
        if (result.ResultCode == ChangeUsagePriceResultCode.Success)
            _telemetry.Increment(BillingMetricNames.Prices.PRICE_CLEARED);
        return result;
    }
}
