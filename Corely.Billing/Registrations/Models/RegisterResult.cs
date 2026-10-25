namespace Corely.Billing.Registrations.Models;

public enum RegisterResultCode
{
    Registered,
    AlreadyRegisteredError,
    NoCapacityError,
    NoActiveGrantError,
    WrongKindError,
    ValidationError,
    UnauthorizedError,
}

public record RegisterResult(
    RegisterResultCode ResultCode,
    string Message,
    Registration? Registration = null
);
