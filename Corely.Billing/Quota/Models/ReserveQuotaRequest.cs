using Corely.Billing.Usage;

namespace Corely.Billing.Quota.Models;

public record ReserveQuotaRequest(
    Guid AccountId,
    UsageOperation Operation,
    UsageUnit Unit,
    long Quantity,
    string Provider,
    Guid? UserId = null,
    Dictionary<string, string>? Tags = null
);
