using Corely.Billing.Usage;

namespace Corely.Billing.Consumption.Models;

/// <summary>
/// The window, bucketing and filters for a consumption time series.
/// </summary>
/// <remarks>
/// Eight positional parameters, of which two adjacent ones are <see cref="DateTime"/> and four
/// adjacent ones are optional lists. Swapping <c>fromUtc</c> and <c>toUtc</c> compiles cleanly and
/// silently returns nothing; passing <c>providers</c> where <c>grantIds</c> belongs does not even
/// compile, but passing the wrong list of the same type does. Naming them removes both hazards.
/// </remarks>
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
