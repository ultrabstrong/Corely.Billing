namespace Corely.Billing.Consumption.Models;

/// <summary>
/// How a reservation resolved. Null while it is still outstanding.
/// </summary>
/// <remarks>
/// Paired with a single nullable <c>FinalizedUtc</c> -- <em>when</em> it resolved -- rather than one
/// timestamp per outcome. Two nullable timestamps would admit a row with both set, which means
/// nothing and becomes an invariant somebody has to remember.
/// </remarks>
public enum ConsumptionOutcome
{
    /// <summary>The work happened and the quantity was corrected to what it actually cost.</summary>
    Settled = 0,

    /// <summary>
    /// The work failed terminally and the hold was given back. The row keeps its original quantity,
    /// so what was held and for how long stays visible during a billing dispute. Encoding a release
    /// as <c>Quantity = 0</c> would destroy exactly that.
    /// </summary>
    Released = 1,
}
