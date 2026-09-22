namespace Corely.Billing.Grants.Models;

public enum GetActiveGrantsResultCode
{
    Success = 0,
    Unauthorized = 1,
}

public record GetActiveGrantsResult(
    GetActiveGrantsResultCode ResultCode,
    string? Message,
    List<Grant>? Grants = null
);
