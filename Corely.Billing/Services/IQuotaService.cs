using Corely.Billing.Quota.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Services;

/// <summary>
/// Holds quota before work, settles it against what the work cost, and gives it back if the work
/// failed. Every call runs inside an operation scope; see <c>IOperationContextAccessor</c>.
/// </summary>
public interface IQuotaService
{
    /// <summary>
    /// Whether the account has any quota at all, without a quantity.
    /// </summary>
    /// <remarks>
    /// The obvious-no check, not the accurate one. It exists so a pipeline can refuse work up front
    /// for an account that could never pay for any of it. Returns <see cref="QuotaAvailability.Unknown"/>
    /// rather than failing when it cannot tell.
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
