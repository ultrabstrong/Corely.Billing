using Corely.Billing.Consumption.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Consumption.Processors;

/// <summary>
/// Writes the consumption ledger. Append-only: rows are added and resolved, never deleted.
/// </summary>
/// <remarks>
/// <para>
/// Reserve before the work, settle after it, release if it fails terminally. The reservation is what
/// closes the race that a read-then-write check cannot: two pieces of work against the same nearly
/// exhausted grant both pass the check, and overspend is bounded only by how many are running.
/// </para>
/// <para>
/// Rows, not grants. Which grants a quantity is drawn from is a quota decision, so settlement is told
/// the per-grant split rather than working it out.
/// </para>
/// <para>
/// No method takes a correlation id, an idempotency key or a reservation handle. Settle, release and
/// the outstanding lookup find their own rows from the ambient operation context that reserve wrote
/// them under.
/// </para>
/// </remarks>
internal interface IConsumptionProcessor
{
    /// <summary>Holds quota against a grant, before the work that will spend it.</summary>
    Task<ReserveConsumptionResult> ReserveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    );

    /// <summary>
    /// Resolves this operation's reservations against what the work actually cost, per grant.
    /// </summary>
    /// <param name="quantityByGrant">
    /// The final charge for each grant the work drew on. A grant already holding a reservation has it
    /// corrected to this number; a grant with none gets a settled row, which is how work that outgrew
    /// its first grant charges the next one. Any reservation on a grant absent from this map is
    /// released.
    /// </param>
    Task<ResolveConsumptionResult> SettleAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        IReadOnlyDictionary<Guid, long> quantityByGrant,
        CancellationToken ct = default
    );

    /// <summary>
    /// Gives back this operation's outstanding reservations. A replay under the same idempotency key
    /// holds the released row again, so releasing does not make a later retry unsafe.
    /// </summary>
    Task<ResolveConsumptionResult> ReleaseAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    );

    /// <summary>
    /// This operation's reservations that are not yet resolved. Null when there is no ambient
    /// operation context to find them by.
    /// </summary>
    Task<List<ConsumptionEvent>?> ListOutstandingReservationsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    );
}
