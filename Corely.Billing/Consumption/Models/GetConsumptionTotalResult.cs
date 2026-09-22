namespace Corely.Billing.Consumption.Models;

public enum GetConsumptionTotalResultCode
{
    Success = 0,
    Unauthorized = 1,
}

public record GetConsumptionTotalResult(
    GetConsumptionTotalResultCode ResultCode,
    string? Message,
    long Total = 0
);
