namespace Corely.Billing.Consumption.Models;

public enum GetGrantConsumptionTotalsResultCode
{
    Success = 0,
    Unauthorized = 1,
}

public record GetGrantConsumptionTotalsResult(
    GetGrantConsumptionTotalsResultCode ResultCode,
    string? Message,
    List<GrantTotalConsumptions>? Totals = null
);
