using Corely.Billing.Usage;

namespace Corely.Billing.GrantRequests.Models;

public record RequestGrantRequest(
    Guid AccountId,
    UsageOperation Operation,
    long? Quantity,
    string? Note = null,
    Guid? RequestedByUserId = null,
    Dictionary<string, string>? Tags = null
);
