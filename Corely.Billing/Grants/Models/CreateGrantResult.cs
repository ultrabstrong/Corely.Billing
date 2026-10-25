namespace Corely.Billing.Grants.Models;

public enum CreateGrantResultCode
{
    Success,
    ValidationError,
    DeclarationMismatchError,
    UnauthorizedError,
}

public record CreateGrantResult(CreateGrantResultCode ResultCode, string Message, Guid CreatedId);
