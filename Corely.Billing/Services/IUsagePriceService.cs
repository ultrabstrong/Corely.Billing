using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Services;

public interface IUsagePriceService
{
    PriceSource Source { get; }

    Task<Money?> GetPriceAsync(UsageOperation operation, CancellationToken ct = default);

    Task<IReadOnlyList<UsagePrice>> ListPricesAsync(CancellationToken ct = default);

    Task<ChangeUsagePriceResult> SetPriceAsync(UsagePrice price, CancellationToken ct = default);

    Task<ChangeUsagePriceResult> ClearPriceAsync(
        UsageOperation operation,
        CancellationToken ct = default
    );
}
