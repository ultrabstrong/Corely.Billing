using Corely.Billing.Usage;

namespace Corely.Billing.Consumption.Models;

public sealed record GetConsumptionTimeSeriesRequest(
    Guid AccountId,
    DateTime FromUtc,
    DateTime ToUtc,
    TimeBucket Bucket,
    IReadOnlyList<UsageUnit>? Units = null,
    IReadOnlyList<UsageOperation>? Operations = null,
    IReadOnlyList<string>? Providers = null,
    IReadOnlyList<Guid>? GrantIds = null
);
