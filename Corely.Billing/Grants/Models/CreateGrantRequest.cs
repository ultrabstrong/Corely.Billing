using Corely.Billing.Usage;

namespace Corely.Billing.Grants.Models;

public record CreateGrantRequest(
    Guid AccountId,
    UsageOperation Operation,
    GrantKind Kind,
    long? Quantity,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    UsageUnit? Unit = null,
    Dictionary<string, string>? Tags = null
);
