using Corely.Billing.Consumption.Extensions;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;

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
                g.AllowanceAndExpiry(vocabulary),
                LiveQuantity(buckets, bucket, [g])
            ))
            .ToList();
        if (folded.Count > 0)
            capacity.Add(new CapacitySeries(OTHER_GRANTS, LiveQuantity(buckets, bucket, folded)));

        return new UsageChartModel(
            [.. buckets.Select(b => bucket.PeriodLabel(b.BucketStart))],
            [.. buckets.Select(b => b.TotalQuantity)],
            capacity,
            relevant.Any(g => g.Quantity is null)
        );
    }

    private static List<long> LiveQuantity(
        IReadOnlyList<ConsumptionTimeBucketData> buckets,
        TimeBucket bucket,
        List<Grant> grants
    ) =>
        [
            .. buckets.Select(b =>
            {
                var end = bucket.NextBucketStart(b.BucketStart);
                return grants
                    .Where(g => g.ValidFromUtc < end && g.ValidToUtc >= b.BucketStart)
                    .Sum(g => g.Quantity!.Value);
            }),
        ];
}
