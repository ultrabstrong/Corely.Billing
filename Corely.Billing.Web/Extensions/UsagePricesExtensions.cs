using Corely.Billing.Prices.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.Extensions;

internal static class UsagePricesExtensions
{
    extension(IReadOnlyList<UsagePrice> prices)
    {
        public IReadOnlyDictionary<UsageOperation, Money?> ByOperation() =>
            prices.ToDictionary(p => p.Operation, p => (Money?)p.UnitPrice);
    }
}
