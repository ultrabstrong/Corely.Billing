namespace Corely.Billing.GrantRequests.Models;

public enum DecideGrantRequestResultCode
{
    Success,
    NotFoundError,
    AlreadyDecidedError,
    ValidationError,
    UnauthorizedError,
}
