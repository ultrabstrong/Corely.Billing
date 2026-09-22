using Corely.Billing;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.Quota.Services;

/// <summary>
/// Quota in terms of grants: which ones a piece of work draws on, and what it finally cost them.
/// </summary>
/// <remarks>
/// The split against metering is by what each layer understands. This one knows grants, their order
/// and their capacity; <c>IConsumptionWriter</c> knows rows. So settlement is worked out here and
/// handed down as a per-grant split, rather than metering inferring anything about grants.
/// </remarks>
public interface IQuotaService
{
    /// <summary>
    /// Whether the account has any quota at all, without a document or a quantity.
    /// </summary>
    /// <remarks>
    /// The obvious-no check, not the accurate one. It exists so a five-hundred-page document is not
    /// split into seventeen children, each with its own blob and job, for an account that could
    /// never have processed the first of them. Quantity cannot be judged here -- that needs a page
    /// count, which needs the document.
    /// </remarks>
    Task<QuotaAvailability> GetAvailabilityAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    );

    Task<ReserveQuotaResult> ReserveAsync(
        ReserveQuotaRequest request,
        CancellationToken ct = default
    );

    /// <summary>Resolves the reservation against what the work actually cost.</summary>
    Task<SettleQuotaResult> SettleAsync(SettleQuotaRequest request, CancellationToken ct = default);

    /// <summary>Gives the hold back, for work that failed terminally.</summary>
    Task<SettleQuotaResult> ReleaseAsync(
        ReleaseQuotaRequest request,
        CancellationToken ct = default
    );
}
