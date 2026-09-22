namespace Corely.Billing.Grants.Models;

public enum GetAllGrantsResultCode
{
    Success = 0,
    Unauthorized = 1,
}

public record GetAllGrantsResult(
    GetAllGrantsResultCode ResultCode,
    string? Message,
    List<Grant>? Grants = null
);
