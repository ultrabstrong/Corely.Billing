namespace Corely.Billing.Quota.Models;

/// <summary>
/// Whether an account has any quota left at all, asked without a document in hand.
/// </summary>
/// <remarks>
/// Deliberately three states rather than a bool. The caller is the workflow initializer, which has
/// to stay near-infallible: it refuses work only on <see cref="Exhausted"/>, and an entitlements
/// database that cannot be reached produces <see cref="Unknown"/> and lets the job start. Getting
/// that wrong would mean a transient database blip stopping every document in the system, to save
/// the pipeline from a rejection it would have reached a few seconds later anyway.
/// </remarks>
public enum QuotaAvailability
{
    /// <summary>Could not be determined. Proceed; the accurate check happens at the step.</summary>
    Unknown = 0,

    /// <summary>At least some room across the account's live grants.</summary>
    Available = 1,

    /// <summary>No live grant with anything left. Nothing this account sends can be processed.</summary>
    Exhausted = 2,
}
