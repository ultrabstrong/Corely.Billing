using Corely.Billing.Usage;

namespace Corely.Billing.Registrations.Models;

public record RegisterRequest(
    Guid AccountId,
    UsageOperation Operation,
    string? Reference = null,
    string? Description = null
);
