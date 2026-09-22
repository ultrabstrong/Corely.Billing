namespace Corely.Billing.Grants.Models;

public enum DeleteGrantResultCode
{
    Success = 0,
    Unauthorized = 1,
    NotFound = 2,
}

public record DeleteGrantResult(DeleteGrantResultCode ResultCode, string? Message);
