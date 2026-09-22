using System.Text.Json;
using Corely.Billing;
using Corely.Billing.Consumption.DataAccess;
using Corely.Billing.Consumption.Models;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Corely.Billing.Consumption.Services;

internal class ConsumptionReader(
    IReadonlyRepo<ConsumptionEventEntity> consumptionEventRepo,
    IOperationContextAccessor operationContextAccessor,
    IOptions<MeteringOptions> meteringOptions,
    TimeProvider timeProvider
) : IConsumptionReader
{
    /// <summary>
    /// The cutoff before which an unresolved reservation has stopped holding quota.
    /// </summary>
    private DateTime LiveReservationsFromUtc =>
        timeProvider.GetUtcNow().UtcDateTime - meteringOptions.Value.ReservationTtl;

    /// <summary>
    /// Narrows to the rows that represent quota actually spoken for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Settled counts. Released does not -- the hold was given back when the step failed
    /// terminally. An unresolved reservation counts while it is inside the TTL and stops counting
    /// once past it.
    /// </para>
    /// <para>
    /// Counting only settled rows is what let two extractions against the same nearly exhausted
    /// grant both pass the check; counting every unresolved row would let a process killed
    /// mid-flight hold quota for ever.
    /// </para>
    /// </remarks>
    private static IQueryable<ConsumptionEventEntity> WhereCounted(
        IQueryable<ConsumptionEventEntity> query,
        DateTime liveFromUtc
    ) =>
        query.Where(c =>
            c.Outcome == ConsumptionOutcome.Settled
            || (c.FinalizedUtc == null && c.UtcTimestamp >= liveFromUtc)
        );

    public async Task<GetConsumptionTotalResult> GetConsumptionTotalAsync(
        Guid accountId,
        UsageUnit unit,
        UsageOperation operation,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? provider = null,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken ct = default
    )
    {
        var sum =
            await consumptionEventRepo.EvaluateAsync(
                (q, ct) =>
                {
                    q = q.Where(e =>
                        e.AccountId == accountId && e.Unit == unit && e.Operation == operation
                    );

                    if (fromUtc is not null)
                    {
                        q = q.Where(e => e.UtcTimestamp >= fromUtc);
                    }

                    if (toUtc is not null)
                    {
                        q = q.Where(e => e.UtcTimestamp <= toUtc);
                    }

                    if (!string.IsNullOrWhiteSpace(provider))
                    {
                        q = q.Where(e => e.Provider == provider);
                    }

                    return WhereCounted(q, LiveReservationsFromUtc)
                        .SumAsync(e => (long?)e.Quantity, ct);
                },
                ct
            ) ?? 0L;

        return new GetConsumptionTotalResult(GetConsumptionTotalResultCode.Success, null, sum);
    }

    public async Task<int> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var liveFromUtc = LiveReservationsFromUtc;

        return await consumptionEventRepo.EvaluateAsync(
            (q, ct) =>
                q.Where(c =>
                        c.AccountId == accountId
                        && c.FinalizedUtc == null
                        && c.UtcTimestamp < liveFromUtc
                    )
                    .CountAsync(ct),
            ct
        );
    }

    public async Task<GetOutstandingReservationsResult> GetOutstandingReservationsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        CancellationToken ct = default
    )
    {
        var operationContext = operationContextAccessor.Current;
        if (operationContext is null)
        {
            return new GetOutstandingReservationsResult(
                GetOutstandingReservationsResultCode.Failed,
                "No ambient operation context."
            );
        }

        var prefix = IdempotencyKeyFactory.Prefix(operationContext, operation, unit);
        var outstanding = await RetryPolicy.ExecuteAsync(
            token =>
                consumptionEventRepo.ListAsync(
                    e =>
                        e.AccountId == accountId
                        && e.FinalizedUtc == null
                        && e.IdempotencyKey.StartsWith(prefix),
                    cancellationToken: token
                ),
            new RetryOptions { ShouldRetry = ex => ex is not OperationCanceledException },
            ct
        );

        return new GetOutstandingReservationsResult(
            GetOutstandingReservationsResultCode.Success,
            null,
            [.. outstanding.Select(ConsumptionEvent.FromEntity)]
        );
    }

    public async Task<GetGrantConsumptionTotalsResult> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        Guid[] grantIds,
        CancellationToken ct = default
    )
    {
        var liveFromUtc = LiveReservationsFromUtc;

        var results = await consumptionEventRepo.EvaluateAsync(
            (q, ct) =>
            {
                q = q.Where(c => c.AccountId == accountId && grantIds.Contains(c.GrantId));
                q = WhereCounted(q, liveFromUtc);

                return q.GroupBy(c => c.GrantId)
                    .Select(g => new GrantTotalConsumptions(
                        g.Key,
                        g.Sum(c => (long?)c.Quantity) ?? 0L
                    ))
                    .ToListAsync(ct);
            },
            ct
        );

        return new GetGrantConsumptionTotalsResult(
            GetGrantConsumptionTotalsResultCode.Success,
            null,
            results
        );
    }

    public async Task<GetConsumptionTimeSeriesResult> GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    )
    {
        var (accountId, fromUtc, toUtc, bucket, units, operations, providers, grantIds) = request;

        var raw = await consumptionEventRepo.EvaluateAsync(
            (q, ct) =>
            {
                q = q.Where(e =>
                    e.AccountId == accountId && e.UtcTimestamp >= fromUtc && e.UtcTimestamp <= toUtc
                );
                q = ApplyOptionalFilters(q, units, operations, providers, grantIds);
                return WhereCounted(q, LiveReservationsFromUtc)
                    .Select(e => new { e.UtcTimestamp, e.Quantity })
                    .ToListAsync(ct);
            },
            ct
        );

        var buckets = raw.GroupBy(e => TruncateToBucket(e.UtcTimestamp, bucket))
            .Select(g => new ConsumptionTimeBucketData(g.Key, g.Sum(e => e.Quantity)))
            .ToDictionary(b => b.BucketStart, b => b.TotalQuantity);

        // Fill in empty buckets so the chart has a continuous x-axis
        var allBuckets = new List<ConsumptionTimeBucketData>();
        for (
            var dt = TruncateToBucket(fromUtc, bucket);
            dt <= toUtc;
            dt = AdvanceBucket(dt, bucket)
        )
        {
            allBuckets.Add(new ConsumptionTimeBucketData(dt, buckets.GetValueOrDefault(dt, 0L)));
        }

        return new GetConsumptionTimeSeriesResult(
            GetConsumptionTimeSeriesResultCode.Success,
            null,
            allBuckets
        );
    }

    public async Task<ListConsumptionEventsResult> ListConsumptionEventsAsync(
        Guid accountId,
        int skip,
        int take,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        IReadOnlyList<UsageUnit>? units = null,
        IReadOnlyList<UsageOperation>? operations = null,
        IReadOnlyList<string>? providers = null,
        IReadOnlyList<Guid>? grantIds = null,
        string? sortColumn = null,
        bool sortDescending = false,
        CancellationToken ct = default
    )
    {
        var (items, totalCount) = await consumptionEventRepo.EvaluateAsync(
            async (q, ct) =>
            {
                q = q.Where(e => e.AccountId == accountId);
                q = ApplyOptionalFilters(q, units, operations, providers, grantIds);

                if (fromUtc is not null)
                    q = q.Where(e => e.UtcTimestamp >= fromUtc);
                if (toUtc is not null)
                    q = q.Where(e => e.UtcTimestamp <= toUtc);

                var count = await q.CountAsync(ct);

                q = ApplySort(q, sortColumn, sortDescending);

                var entities = await q.Skip(skip).Take(take).ToListAsync(ct);
                var summaries = entities
                    .Select(e => new ConsumptionEventSummary(
                        e.ConsumptionId,
                        e.Quantity,
                        e.Unit,
                        e.Operation,
                        e.Provider,
                        e.UtcTimestamp,
                        e.GrantId,
                        e.Outcome,
                        e.UserId,
                        DeserializeTags(e.TagsJson)
                    ))
                    .ToList();

                return (summaries, count);
            },
            ct
        );

        return new ListConsumptionEventsResult(
            ListConsumptionEventsResultCode.Success,
            null,
            items,
            totalCount
        );
    }

    private static IQueryable<ConsumptionEventEntity> ApplyOptionalFilters(
        IQueryable<ConsumptionEventEntity> q,
        IReadOnlyList<UsageUnit>? units,
        IReadOnlyList<UsageOperation>? operations,
        IReadOnlyList<string>? providers,
        IReadOnlyList<Guid>? grantIds = null
    )
    {
        if (units is { Count: > 0 })
            q = q.Where(e => units.Contains(e.Unit));
        if (operations is { Count: > 0 })
            q = q.Where(e => operations.Contains(e.Operation));
        if (providers is { Count: > 0 })
            q = q.Where(e => providers.Contains(e.Provider));
        if (grantIds is { Count: > 0 })
            q = q.Where(e => grantIds.Contains(e.GrantId));
        return q;
    }

    private static IQueryable<ConsumptionEventEntity> ApplySort(
        IQueryable<ConsumptionEventEntity> q,
        string? sortColumn,
        bool sortDescending
    )
    {
        return sortColumn?.ToLowerInvariant() switch
        {
            "quantity" => sortDescending
                ? q.OrderByDescending(e => e.Quantity)
                : q.OrderBy(e => e.Quantity),
            "unit" => sortDescending ? q.OrderByDescending(e => e.Unit) : q.OrderBy(e => e.Unit),
            "operation" => sortDescending
                ? q.OrderByDescending(e => e.Operation)
                : q.OrderBy(e => e.Operation),
            "provider" => sortDescending
                ? q.OrderByDescending(e => e.Provider)
                : q.OrderBy(e => e.Provider),
            _ => sortDescending
                ? q.OrderByDescending(e => e.UtcTimestamp)
                : q.OrderBy(e => e.UtcTimestamp),
        };
    }

    private static DateTime TruncateToBucket(DateTime dt, TimeBucket bucket) =>
        bucket switch
        {
            TimeBucket.Day => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc),
            TimeBucket.Week => StartOfWeek(dt),
            TimeBucket.Month => new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc),
        };

    private static DateTime AdvanceBucket(DateTime dt, TimeBucket bucket) =>
        bucket switch
        {
            TimeBucket.Day => dt.AddDays(1),
            TimeBucket.Week => dt.AddDays(7),
            TimeBucket.Month => dt.AddMonths(1),
            _ => dt.AddDays(1),
        };

    private static DateTime StartOfWeek(DateTime dt)
    {
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-diff);
    }

    public async Task<List<string>> GetDistinctProvidersAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        await consumptionEventRepo.EvaluateAsync(
            (q, ct) =>
                q.Where(e => e.AccountId == accountId)
                    .Select(e => e.Provider)
                    .Distinct()
                    .OrderBy(p => p)
                    .ToListAsync(ct),
            ct
        );

    public async Task<DateTime?> GetEarliestEventDateAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        await consumptionEventRepo.EvaluateAsync(
            (q, ct) =>
                q.Where(e => e.AccountId == accountId)
                    .OrderBy(e => e.UtcTimestamp)
                    .Select(e => (DateTime?)e.UtcTimestamp)
                    .FirstOrDefaultAsync(ct),
            ct
        );

    private static IReadOnlyDictionary<string, string>? DeserializeTags(string? tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson);
        }
        catch
        {
            return null;
        }
    }
}
