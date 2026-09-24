using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.Components;

internal sealed record CapacitySeries(string Label, IReadOnlyList<long> Data);

internal sealed record UsageChartModel(
    IReadOnlyList<string> Labels,
    IReadOnlyList<long> Consumption,
    IReadOnlyList<CapacitySeries> Capacity,
    bool HasUnlimitedGrant
)
{
    public const int MAX_CAPACITY_SERIES = 3;
    public const string OTHER_GRANTS = "Other grants";

    public bool IsEmpty => Consumption.All(q => q == 0) && Capacity.Count == 0;

    public static TimeBucket BucketFor(DateTime fromUtc, DateTime toUtc) =>
        (toUtc - fromUtc).TotalDays switch
        {
            <= 31 => TimeBucket.Day,
            <= 120 => TimeBucket.Week,
            _ => TimeBucket.Month,
        };

    public static UsageChartModel Build(
        IReadOnlyList<ConsumptionTimeBucketData> buckets,
        IEnumerable<Grant> grants,
        TimeBucket bucket,
        UsageFilter filter,
        IUsageVocabulary vocabulary
    )
    {
        var relevant = grants
            .Where(g => filter.Units is not { Count: > 0 } || filter.Units.Contains(g.Unit))
            .Where(g =>
                filter.Operations is not { Count: > 0 } || filter.Operations.Contains(g.Operation)
            )
            .Where(g =>
                filter.GrantIds is not { Count: > 0 } || filter.GrantIds.Contains(g.GrantId)
            )
            .Where(g => g.ValidFromUtc <= filter.ToUtc && g.ValidToUtc >= filter.FromUtc)
            .ToList();

        var limited = relevant
            .Where(g => g.Quantity is not null)
            .OrderBy(g => g.ValidFromUtc)
            .ThenBy(g => g.GrantId)
            .ToList();

        var named = limited.Take(MAX_CAPACITY_SERIES).ToList();
        var folded = limited.Skip(MAX_CAPACITY_SERIES).ToList();

        var capacity = named
            .Select(g => new CapacitySeries(
                GrantLabel(g, vocabulary),
                LiveQuantity(buckets, bucket, [g])
            ))
            .ToList();
        if (folded.Count > 0)
            capacity.Add(new CapacitySeries(OTHER_GRANTS, LiveQuantity(buckets, bucket, folded)));

        return new UsageChartModel(
            [.. buckets.Select(b => BucketLabel(b.BucketStart, bucket))],
            [.. buckets.Select(b => b.TotalQuantity)],
            capacity,
            relevant.Any(g => g.Quantity is null)
        );
    }

    public static string BucketLabel(DateTime start, TimeBucket bucket) =>
        bucket switch
        {
            TimeBucket.Week => $"Week of {start:MMM d}",
            TimeBucket.Month => start.ToString("MMM yyyy"),
            _ => start.ToString("MMM d"),
        };

    private static string GrantLabel(Grant grant, IUsageVocabulary vocabulary) =>
        $"{UsageText.Count(grant.Quantity, vocabulary.DisplayName(grant.Unit))} to {grant.ValidToUtc:MMM d, yyyy}";

    private static List<long> LiveQuantity(
        IReadOnlyList<ConsumptionTimeBucketData> buckets,
        TimeBucket bucket,
        List<Grant> grants
    ) =>
        [
            .. buckets.Select(b =>
            {
                var end = End(b.BucketStart, bucket);
                return grants
                    .Where(g => g.ValidFromUtc < end && g.ValidToUtc >= b.BucketStart)
                    .Sum(g => g.Quantity!.Value);
            }),
        ];

    private static DateTime End(DateTime start, TimeBucket bucket) =>
        bucket switch
        {
            TimeBucket.Week => start.AddDays(7),
            TimeBucket.Month => start.AddMonths(1),
            _ => start.AddDays(1),
        };
}
