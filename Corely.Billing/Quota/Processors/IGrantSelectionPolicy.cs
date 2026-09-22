using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.Quota.Processors;

/// <summary>
/// Decides which grants a quantity of work is drawn from, and in what order.
/// </summary>
internal interface IGrantSelectionPolicy
{
    /// <summary>
    /// Spreads <paramref name="quantity"/> across the grants that have room for it.
    /// </summary>
    /// <remarks>
    /// Quantity-aware on purpose. Selecting a single grant on "has any room left" charges a
    /// five-hundred-unit piece of work entirely to a grant with one unit remaining: nothing blocks, so
    /// it is invisible day to day, and what it produces is a grant reading 500/1 consumed next to an
    /// untouched one. Reporting the shortfall rather than refusing lets the caller decide, because
    /// before the work and after it that shortfall means opposite things.
    /// </remarks>
    GrantSplit Split(List<Grant> grants, List<GrantTotalConsumptions> grantTotals, long quantity);
}
