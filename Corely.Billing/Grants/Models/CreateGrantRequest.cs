using Corely.Billing.Usage;

namespace Corely.Billing.Grants.Models;

public record CreateGrantRequest(
    Guid AccountId,
    UsageOperation Operation,
    UsageUnit Unit,
    long? Quantity,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    Dictionary<string, string>? Tags = null
);
