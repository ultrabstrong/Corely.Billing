namespace Corely.Billing.Consumption.Models;

public sealed record ConsumptionSeries(
    string? Key,
    IReadOnlyList<ConsumptionTimeBucketData> Buckets
)
{
    public long Total => Buckets.Sum(b => b.TotalQuantity);
}
