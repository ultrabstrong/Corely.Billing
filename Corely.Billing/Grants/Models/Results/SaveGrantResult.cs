namespace Corely.Billing.Grants.Models;

public enum SaveGrantResultCode
{
    Success = 0,
    Unauthorized = 1,
    UnknownUsage = 2,
}

public record SaveGrantResult(SaveGrantResultCode ResultCode, string? Message);
