using Corely.Billing;
using Corely.Billing.Consumption.Models;

namespace Corely.Billing.Consumption.Services;

/// <summary>
/// Writes the consumption ledger. Append-only: rows are added and resolved, never deleted.
/// </summary>
/// <remarks>
/// <para>
/// Reserve before the work, settle after it, release if it fails terminally. The reservation is what
/// closes the race that a read-then-write check cannot: two extractions against the same nearly
/// exhausted grant both used to pass the check, and overspend was bounded only by how many consumers
/// happened to be running.
/// </para>
/// <para>
/// Rows, not grants. Which grants a quantity is drawn from is a quota decision, so settlement is told
/// the per-grant split rather than working it out -- see <c>IQuotaService</c>.
/// </para>
/// <para>
/// No method here takes a correlation id, an idempotency key or a reservation handle. Callers know
/// nothing about any of that; settle, release and the outstanding lookup find their own rows from the
/// ambient operation context that reserve wrote them under. The account id they do take is the
/// caller's own, not a metering detail, and it keeps those lookups on the
/// <c>(AccountId, IdempotencyKey)</c> index instead of scanning the whole ledger.
/// </para>
/// </remarks>
public interface IConsumptionWriter
{
    /// <summary>Records a charge that is already final. For work that was never reserved.</summary>
    Task<SaveConsumptionResult> SaveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    );

    /// <summary>Holds quota against a grant, before the work that will spend it.</summary>
    Task<SaveConsumptionResult> ReserveAsync(
        ConsumptionEvent consumptionEvent,
        CancellationToken ct = default
    );

    /// <summary>
    /// Resolves this operation's reservations against what the work actually cost, per grant.
    /// </summary>
    /// <param name="quantityByGrant">
    /// The final charge for each grant the work drew on. A grant already holding a reservation has it
    /// corrected to this number; a grant with none gets a settled row, which is how a document that
    /// outgrew its first grant charges the next one. Any reservation on a grant absent from this map
    /// is released.
    /// </param>
    Task<SettleConsumptionResult> SettleAsync(
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
    Task<SettleConsumptionResult> ReleaseAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    );
}
