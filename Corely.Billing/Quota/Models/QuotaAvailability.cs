namespace Corely.Billing.Quota.Models;

/// <summary>
/// Whether an account has any quota left at all, asked before the size of the work is known.
/// </summary>
/// <remarks>
/// Deliberately three states rather than a bool. The caller is usually the front door of a pipeline,
/// which has to stay near-infallible: it refuses work only on <see cref="Exhausted"/>, and a database
/// that cannot be reached produces <see cref="Unknown"/> and lets the work start. Getting that wrong
/// would mean a transient database blip stopping all work, to save a rejection that reservation would
/// have reached a few seconds later anyway.
/// </remarks>
public enum QuotaAvailability
{
    /// <summary>Could not be determined. Proceed; the accurate check happens at reservation.</summary>
    Unknown,

    /// <summary>At least some room across the account's live grants.</summary>
    Available,

    /// <summary>No live grant with anything left. Nothing this account sends can be processed.</summary>
    Exhausted,
}
