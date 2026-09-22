namespace Corely.Billing.Grants.Models;

public enum GetGrantByIdResultCode
{
    Success = 0,
    Unauthorized = 1,
    NotFound = 2,
}

public record GetGrantByIdResult(
    GetGrantByIdResultCode ResultCode,
    string? Message,
    Grant? Grant = null
);
