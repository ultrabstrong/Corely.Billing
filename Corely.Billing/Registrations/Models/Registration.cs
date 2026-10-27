using Corely.Billing.Registrations.Enums;
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
)
{
    internal const int REFERENCE_MAX_LENGTH = 200;
    internal const int DESCRIPTION_MAX_LENGTH = 500;
}
