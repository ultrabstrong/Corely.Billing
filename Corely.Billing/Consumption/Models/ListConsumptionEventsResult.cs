namespace Corely.Billing.Consumption.Models;

public enum ListConsumptionEventsResultCode
{
    Success = 0,
    Unauthorized = 1,
}

public record ListConsumptionEventsResult(
    ListConsumptionEventsResultCode ResultCode,
    string? Message,
    List<ConsumptionEventSummary>? Items = null,
    int TotalCount = 0
);
