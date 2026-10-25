namespace Corely.Billing.Registrations.Models;

public record MoveRegistrationsRequest(
    Guid AccountId,
    Guid FromGrantId,
    Guid ToGrantId,
    IReadOnlyList<Guid>? RegistrationIds = null
);
