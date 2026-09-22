namespace Corely.Billing.Consumption.Models;

public enum GetConsumptionTimeSeriesResultCode
{
    Success = 0,
    Unauthorized = 1,
}

public record GetConsumptionTimeSeriesResult(
    GetConsumptionTimeSeriesResultCode ResultCode,
    string? Message,
    List<ConsumptionTimeBucketData>? Buckets = null
);
