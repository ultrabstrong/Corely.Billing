using Corely.Billing.Usage;

namespace Corely.Billing.Registrations.Models;

public record Registration(
    Guid RegistrationId,
    Guid AccountId,
    Guid GrantId,
    UsageOperation Operation,
    string? Reference,
    string? Description,
    DateTime RegisteredUtc,
    DateTime? ReleasedUtc,
    RegistrationStatus Status
);
