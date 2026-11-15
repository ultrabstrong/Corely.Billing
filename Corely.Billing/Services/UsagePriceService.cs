using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Prices.Processors;
using Corely.Billing.Usage;
using Corely.Common.Extensions;

namespace Corely.Billing.Services;

internal class UsagePriceService(IUsagePriceProcessor priceProcessor) : IUsagePriceService
{
    private readonly IUsagePriceProcessor _priceProcessor = priceProcessor.ThrowIfNull(
        nameof(priceProcessor)
    );

    public PriceSource Source => _priceProcessor.Source;

    public Task<Money?> GetPriceAsync(UsageOperation operation, CancellationToken ct = default) =>
        _priceProcessor.GetPriceAsync(operation, ct);

    public Task<IReadOnlyList<UsagePrice>> ListPricesAsync(CancellationToken ct = default) =>
        _priceProcessor.ListPricesAsync(ct);

    public Task<ChangeUsagePriceResult> SetPriceAsync(
        UsagePrice price,
        CancellationToken ct = default
    ) => _priceProcessor.SetPriceAsync(price, ct);

    public Task<ChangeUsagePriceResult> ClearPriceAsync(
        UsageOperation operation,
        CancellationToken ct = default
    ) => _priceProcessor.ClearPriceAsync(operation, ct);
}
