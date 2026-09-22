using Corely.Billing.Usage;

namespace Corely.Billing.Quota.Models;

/// <summary>
/// Everything needed to hold quota for a piece of work, before doing it.
/// </summary>
/// <param name="Quantity">
/// What the work is expected to cost. The floor is a legitimate answer where nothing knows the real
/// number yet -- settlement corrects it.
/// </param>
/// <param name="Provider">Who performs the work, for reporting on consumption by provider.</param>
public record ReserveQuotaRequest(
    Guid AccountId,
    UsageOperation Operation,
    UsageUnit Unit,
    long Quantity,
    string Provider,
    Guid? UserId = null,
    Dictionary<string, string>? Tags = null
);
