using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.Quota.Processors;

/// <summary>
/// Spends perishable quota before durable quota, filling each grant before moving to the next.
/// </summary>
/// <remarks>
/// The ordering is the product decision: a grant that expires sooner is worth less to the customer
/// unspent, so it goes first. Quantity breaks ties, then start date, so the order is total and a
/// replay allocates identically.
/// </remarks>
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
                .OrderBy(g => g.ValidToUtc)
                .ThenBy(g => g.Quantity)
                .ThenBy(g => g.ValidFromUtc)
                .ThenBy(g => g.GrantId)
        )
        {
            if (outstanding == 0)
                break;

            var consumed =
                grantTotals.FirstOrDefault(t => t.GrantId == grant.GrantId)?.TotalConsumedQuantity
                ?? 0L;

            // Already overdrawn grants report a negative remainder, which must not add capacity back.
            var remaining = Math.Max(0, grant.Quantity - consumed);
            if (remaining == 0)
                continue;

            var take = Math.Min(remaining, outstanding);
            shares.Add(new GrantShare(grant.GrantId, take));
            outstanding -= take;
        }

        return new GrantSplit(shares, outstanding);
    }
}
