namespace Corely.Billing.Registrations.Models;

public enum MoveRegistrationsResultCode
{
    Success,
    NotFoundError,
    WrongKindError,
    NoCapacityError,
    UnauthorizedError,
}

public record MoveRegistrationsResult(
    MoveRegistrationsResultCode ResultCode,
    string Message,
    IReadOnlyList<Registration> Moved
);
