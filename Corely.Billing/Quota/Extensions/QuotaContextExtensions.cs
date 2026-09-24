using Corely.Billing.Consumption.Models;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.Quota.Extensions;

internal static class QuotaContextExtensions
{
    extension(QuotaContext context)
    {
        public QuotaContext WithoutHolds(IReadOnlyList<ConsumptionEvent> holds) =>
            context with
            {
                Totals =
                [
                    .. context.Totals.Select(t =>
                        t with
                        {
                            TotalConsumedQuantity =
                                t.TotalConsumedQuantity
                                - holds.Where(h => h.GrantId == t.GrantId).Sum(h => h.Quantity),
                        }
                    ),
                ],
            };

        public double RemainingRatio(long charged)
        {
            if (context.Grants.Any(g => g.Quantity is null))
                return 1;

            var total = context.Grants.Sum(g => g.Quantity!.Value);
            if (total <= 0)
                return 0;

            var consumed = context.Totals.Sum(t => t.TotalConsumedQuantity) + charged;
            return Math.Clamp((double)(total - consumed) / total, 0, 1);
        }
    }
}
