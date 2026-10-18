namespace Corely.Billing.GrantRequests.Models;

public enum RequestGrantResultCode
{
    Success,
    ValidationError,
    AlreadyPendingError,
    UnauthorizedError,
}

public record RequestGrantResult(RequestGrantResultCode ResultCode, string Message, Guid CreatedId);
