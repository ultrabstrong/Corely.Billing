namespace Corely.Billing.Grants.Models;

public enum DeleteGrantResultCode
{
    Success,
    NotFoundError,
    UnauthorizedError,
}

public record DeleteGrantResult(DeleteGrantResultCode ResultCode, string Message);
