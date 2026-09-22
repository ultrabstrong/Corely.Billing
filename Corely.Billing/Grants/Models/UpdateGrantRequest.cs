using Corely.Billing.Usage;

namespace Corely.Billing.Grants.Models;

/// <remarks>
/// No operation. Consumption is recorded against a grant's operation, so changing it would strand
/// every row already charged to the grant.
/// </remarks>
public record UpdateGrantRequest(
    Guid AccountId,
    Guid GrantId,
    UsageUnit Unit,
    long Quantity,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    Dictionary<string, string>? Tags = null
);
