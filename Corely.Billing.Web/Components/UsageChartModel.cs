using System.Globalization;
using Corely.Billing.Consumption.Extensions;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.Components;

// Overdrawn, where given, is how far past its quantity a grant stood in each period; Data floors
// at zero, so the chart would otherwise lose it.
internal sealed record ChartSeries(
    string Key,
    string Label,
    IReadOnlyList<long?> Data,
    bool IsOther = false,
    IReadOnlyList<long>? Overdrawn = null
);

internal sealed record CapacitySeries(string Label, IReadOnlyList<long> Data);

internal sealed record UsageChartModel(
    UsageChartView View,
    UsageUnit? Unit,
    string UnitName,
    IReadOnlyList<DateTime> PeriodStarts,
    IReadOnlyList<string> Labels,
    IReadOnlyList<ChartSeries> Series,
    IReadOnlyList<CapacitySeries> Capacity,
    bool HasUnlimitedGrant
)
{
    public const int MAX_CAPACITY_SERIES = 3;
    public const int MAX_SERIES = 5;
    public const string OTHER = "Other";
    public const string OTHER_GRANTS = "Other grants";
    public const string USED = "Used";
    public const string USED_SO_FAR = "Used so far";
    public const string ALLOWANCE = "Allowance live";
    public const string ALL_LIVE_GRANTS = "All live grants";
    public const string CSV_HEADER = "period_start_utc,period,series_key,series_name,quantity,unit";

    public bool IsEmpty => Series.All(s => s.Data.All(v => v is null or 0)) && Capacity.Count == 0;

    // What the chart adds up to: used across the range, or for the running views, where the first
    // line ends.
    public long Total =>
        View is UsageChartView.BurnUp or UsageChartView.Remaining
            ? Series.FirstOrDefault()?.Data.LastOrDefault(v => v is not null) ?? 0
            : Series.Sum(s => s.Data.Sum() ?? 0);

    public static UsageChartModel Empty(UsageChartView view) =>
        new(view, null, string.Empty, [], [], [], [], false);

    public static TimeBucket BucketFor(DateTime fromUtc, DateTime toUtc) =>
        (toUtc - fromUtc).TotalDays switch
        {
            <= 31 => TimeBucket.Day,
            <= 120 => TimeBucket.Week,
            _ => TimeBucket.Month,
        };

    public static List<Grant> RelevantGrants(
        IEnumerable<Grant> grants,
        UsageFilter filter,
        UsageUnit unit
    ) =>
        [
            .. grants
                .Where(g => g.Unit == unit)
                .Where(g =>
                    filter.Operations is not { Count: > 0 }
                    || filter.Operations.Contains(g.Operation)
                )
                .Where(g =>
                    filter.GrantIds is not { Count: > 0 } || filter.GrantIds.Contains(g.GrantId)
                )
                .Where(g => g.ValidFromUtc <= filter.ToUtc && g.ValidToUtc >= filter.FromUtc),
        ];

    // series is the total for Used, the breakdown for Stacked, SideBySide and Share, the limited
    // grants' charges for BurnUp, and those from before the range starts for Remaining.
    public static UsageChartModel Build(
        UsageChartView view,
        IReadOnlyList<ConsumptionSeries> series,
        IReadOnlyList<Grant> relevant,
        TimeBucket bucket,
        UsageFilter filter,
        UsageUnit unit,
        string unitName,
        Func<string, string> keyLabel,
        Func<Grant, string> grantLabel
    )
    {
        List<DateTime> starts = [];
        for (
            var start = bucket.BucketStart(filter.FromUtc);
            start <= filter.ToUtc;
            start = bucket.NextBucketStart(start)
        )
        {
            starts.Add(start);
        }

        var limited = relevant
            .Where(g => g.Quantity is not null)
            .OrderBy(g => g.ValidFromUtc)
            .ThenBy(g => g.GrantId)
            .ToList();
        var unlimited = relevant.Any(g => g.Quantity is null);

        if (view == UsageChartView.Share)
        {
            var range = $"{filter.FromUtc:MMM d, yyyy} to {filter.ToUtc:MMM d, yyyy}";
            var shares = Fold(
                [.. series.Select(s => new ChartSeries(s.Key!, keyLabel(s.Key!), [s.Total]))],
                OTHER
            );
            return new(view, unit, unitName, [starts[0]], [range], shares, [], unlimited);
        }

        IReadOnlyList<ChartSeries> data = view switch
        {
            UsageChartView.Stacked or UsageChartView.SideBySide => Fold(
                [
                    .. series.Select(s => new ChartSeries(
                        s.Key!,
                        keyLabel(s.Key!),
                        Values(s, starts)
                    )),
                ],
                OTHER
            ),
            UsageChartView.Remaining => Remaining(series, limited, starts, bucket, grantLabel),
            UsageChartView.BurnUp => BurnUp(series, limited, starts, bucket),
            _ => [new ChartSeries(USED, USED, Values(series.FirstOrDefault(), starts))],
        };

        var capacity = view
            is UsageChartView.Used
                or UsageChartView.Stacked
                or UsageChartView.SideBySide
            ? LiveCapacity(limited, starts, bucket, grantLabel)
            : [];

        return new(
            view,
            unit,
            unitName,
            starts,
            [.. starts.Select(s => bucket.PeriodLabel(s))],
            data,
            capacity,
            unlimited
        );
    }

    public string ToCsv()
    {
        var lines = new List<string> { CSV_HEADER };
        foreach (var series in Series)
        {
            for (var i = 0; i < PeriodStarts.Count && i < series.Data.Count; i++)
            {
                lines.Add(
                    new[]
                    {
                        PeriodStarts[i].IsoUtc(),
                        Labels[i],
                        series.Key,
                        series.Label,
                        series.Data[i]?.ToString(CultureInfo.InvariantCulture),
                        Unit?.Value,
                    }.ToCsvRow()
                );
            }
        }
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static List<long?> Values(ConsumptionSeries? series, List<DateTime> starts)
    {
        var sums = series?.Buckets.ToDictionary(b => b.BucketStart, b => b.TotalQuantity) ?? [];
        return [.. starts.Select(s => (long?)sums.GetValueOrDefault(s))];
    }

    // Series arrive ranked by total; past MAX_SERIES they fold into one.
    private static List<ChartSeries> Fold(List<ChartSeries> ranked, string otherLabel)
    {
        if (ranked.Count <= MAX_SERIES)
            return ranked;

        var rest = ranked.Skip(MAX_SERIES).ToList();
        var other = Enumerable
            .Range(0, rest[0].Data.Count)
            .Select(i =>
                rest.Any(s => s.Data[i] is not null) ? rest.Sum(s => s.Data[i] ?? 0) : (long?)null
            )
            .ToList();
        return [.. ranked.Take(MAX_SERIES), new ChartSeries(OTHER, otherLabel, other, true)];
    }

    private static List<ChartSeries> Remaining(
        IReadOnlyList<ConsumptionSeries> byGrant,
        List<Grant> limited,
        List<DateTime> starts,
        TimeBucket bucket,
        Func<Grant, string> grantLabel
    )
    {
        if (limited.Count == 0 || starts.Count == 0)
            return [];

        var buckets = byGrant
            .Where(s => s.Key is not null)
            .ToDictionary(s => s.Key!, s => s.Buckets);
        var perGrant = limited
            .Select(g =>
            {
                var key = g.GrantId.ToString();
                var rows = buckets.GetValueOrDefault(key) ?? [];
                var used = rows.Where(b => b.BucketStart < starts[0]).Sum(b => b.TotalQuantity);
                var moved = rows.ToDictionary(b => b.BucketStart, b => b.TotalQuantity);
                var usedAt = starts.Select(s => used += moved.GetValueOrDefault(s)).ToList();
                var live = starts
                    .Select(s => g.ValidFromUtc < bucket.NextBucketStart(s) && g.ValidToUtc >= s)
                    .ToList();
                var quantity = g.Quantity!.Value;
                return new ChartSeries(
                    key,
                    grantLabel(g),
                    [.. usedAt.Select((u, i) => live[i] ? Math.Max(0, quantity - u) : (long?)null)],
                    Overdrawn: [.. usedAt.Select((u, i) => live[i] ? Math.Max(0, u - quantity) : 0)]
                );
            })
            .ToList();

        var total = Enumerable
            .Range(0, starts.Count)
            .Select(i =>
                perGrant.Any(s => s.Data[i] is not null)
                    ? perGrant.Sum(s => s.Data[i] ?? 0)
                    : (long?)null
            )
            .ToList();

        return
        [
            new ChartSeries(ALL_LIVE_GRANTS, ALL_LIVE_GRANTS, total),
            .. Fold(perGrant, OTHER_GRANTS),
        ];
    }

    // series is what the limited grants were charged, by grant, or with none, the total.
    private static List<ChartSeries> BurnUp(
        IReadOnlyList<ConsumptionSeries> series,
        List<Grant> limited,
        List<DateTime> starts,
        TimeBucket bucket
    )
    {
        long running = 0;
        var perPeriod = series.Select(s => Values(s, starts)).ToList();
        var cumulative = starts
            .Select((_, i) => (long?)(running += perPeriod.Sum(p => p[i] ?? 0)))
            .ToList();
        List<ChartSeries> lines = [new ChartSeries(USED_SO_FAR, USED_SO_FAR, cumulative)];

        if (limited.Count > 0)
        {
            lines.Add(
                new ChartSeries(
                    ALLOWANCE,
                    ALLOWANCE,
                    [.. LiveQuantity(starts, bucket, limited).Select(q => (long?)q)]
                )
            );
        }
        return lines;
    }

    private static List<CapacitySeries> LiveCapacity(
        List<Grant> limited,
        List<DateTime> starts,
        TimeBucket bucket,
        Func<Grant, string> grantLabel
    )
    {
        var capacity = limited
            .Take(MAX_CAPACITY_SERIES)
            .Select(g => new CapacitySeries(grantLabel(g), LiveQuantity(starts, bucket, [g])))
            .ToList();
        var folded = limited.Skip(MAX_CAPACITY_SERIES).ToList();
        if (folded.Count > 0)
            capacity.Add(new CapacitySeries(OTHER_GRANTS, LiveQuantity(starts, bucket, folded)));
        return capacity;
    }

    private static List<long> LiveQuantity(
        List<DateTime> starts,
        TimeBucket bucket,
        List<Grant> grants
    ) =>
        [
            .. starts.Select(s =>
            {
                var end = bucket.NextBucketStart(s);
                return grants
                    .Where(g => g.ValidFromUtc < end && g.ValidToUtc >= s)
                    .Sum(g => g.Quantity!.Value);
            }),
        ];
}
