using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.Quota.Processors;

internal sealed class ExpiringFirstGrantSelectionPolicy : IGrantSelectionPolicy
{
    public GrantSplit Split(
        List<Grant> grants,
        List<GrantTotalConsumptions> grantTotals,
        long quantity
    )
    {
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(grantTotals);
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);

        var shares = new List<GrantShare>();
        var outstanding = quantity;

        foreach (
            var grant in grants
                .OrderBy(g => g.Quantity is not null)
                .ThenBy(g => g.ValidToUtc)
                .ThenBy(g => g.Quantity)
                .ThenBy(g => g.ValidFromUtc)
                .ThenBy(g => g.GrantId)
        )
        {
            if (outstanding == 0)
                break;

            if (grant.Quantity is not { } quantityGranted)
            {
                shares.Add(new GrantShare(grant.GrantId, outstanding));
                return new GrantSplit(shares, 0);
            }

            var consumed =
                grantTotals.FirstOrDefault(t => t.GrantId == grant.GrantId)?.TotalConsumedQuantity
                ?? 0L;

            var remaining = Math.Max(0, quantityGranted - consumed);
            if (remaining == 0)
                continue;

            var take = Math.Min(remaining, outstanding);
            shares.Add(new GrantShare(grant.GrantId, take));
            outstanding -= take;
        }

        return new GrantSplit(shares, outstanding);
    }
}
