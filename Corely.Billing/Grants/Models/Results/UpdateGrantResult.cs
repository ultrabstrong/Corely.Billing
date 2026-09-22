namespace Corely.Billing.Grants.Models;

public enum UpdateGrantResultCode
{
    Success = 0,
    Unauthorized = 1,
    NotFound = 2,
    UnknownUsage = 3,
}

public record UpdateGrantResult(UpdateGrantResultCode ResultCode, string? Message);
