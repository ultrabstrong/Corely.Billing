using Corely.Billing.Usage;

namespace Corely.Billing.Grants.Models;

public record UpdateGrantRequest(
    Guid AccountId,
    Guid GrantId,
    UsageUnit Unit,
    long Quantity,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    Dictionary<string, string>? Tags = null
);
