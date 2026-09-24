using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;

namespace Corely.Billing.Quota.Models;

internal sealed record QuotaContext(List<Grant> Grants, List<GrantTotalConsumptions> Totals)
{
    public QuotaContext WithoutHolds(IReadOnlyList<ConsumptionEvent> holds) =>
        this with
        {
            Totals =
            [
                .. Totals.Select(t =>
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
        if (Grants.Any(g => g.Quantity is null))
            return 1;

        var total = Grants.Sum(g => g.Quantity!.Value);
        if (total <= 0)
            return 0;

        var consumed = Totals.Sum(t => t.TotalConsumedQuantity) + charged;
        return Math.Clamp((double)(total - consumed) / total, 0, 1);
    }
}
