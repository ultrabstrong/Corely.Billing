namespace Corely.Billing.Grants.Models;

public record UpdateGrantRequest(
    Guid AccountId,
    Guid GrantId,
    decimal? Quantity,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    Dictionary<string, string>? Tags = null
);
