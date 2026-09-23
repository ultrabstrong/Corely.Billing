using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Mappers;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Models;
using Corely.Billing.Usage;
using Corely.Common.Extensions;
using Corely.Common.Filtering.Ordering;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Corely.Billing.Consumption.Processors;

internal class ConsumptionReportProcessor(
    IReadonlyRepo<ConsumptionEventEntity> consumptionEventRepo,
    IOptions<ReservationOptions> reservationOptions,
    TimeProvider timeProvider
) : IConsumptionReportProcessor
{
    private readonly IReadonlyRepo<ConsumptionEventEntity> _consumptionEventRepo =
        consumptionEventRepo.ThrowIfNull(nameof(consumptionEventRepo));
    private readonly ReservationOptions _reservationOptions = reservationOptions
        .ThrowIfNull(nameof(reservationOptions))
        .Value;
    private readonly TimeProvider _timeProvider = timeProvider.ThrowIfNull(nameof(timeProvider));

    private DateTime LiveReservationsFromUtc =>
        _timeProvider.GetUtcNow().UtcDateTime - _reservationOptions.ReservationTtl;

    private static IQueryable<ConsumptionEventEntity> WhereCounted(
        IQueryable<ConsumptionEventEntity> query,
        DateTime liveFromUtc
    ) =>
        query.Where(c =>
            c.Outcome == ConsumptionOutcome.Settled
            || (c.FinalizedUtc == null && c.UtcTimestamp >= liveFromUtc)
        );

    public async Task<long> GetConsumptionTotalAsync(
        GetConsumptionTotalRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        var (accountId, operation, unit, fromUtc, toUtc, provider) = request;
        var liveFromUtc = LiveReservationsFromUtc;

        return await _consumptionEventRepo.EvaluateAsync(
                (q, token) =>
                {
                    q = q.Where(e =>
                        e.AccountId == accountId && e.Unit == unit && e.Operation == operation
                    );
                    if (fromUtc is not null)
                        q = q.Where(e => e.UtcTimestamp >= fromUtc);
                    if (toUtc is not null)
                        q = q.Where(e => e.UtcTimestamp <= toUtc);
                    if (!string.IsNullOrWhiteSpace(provider))
                        q = q.Where(e => e.Provider == provider);

                    return WhereCounted(q, liveFromUtc).SumAsync(e => (long?)e.Quantity, token);
                },
                ct
            ) ?? 0L;
    }

    public async Task<List<GrantTotalConsumptions>> GetGrantConsumptionTotalsAsync(
        Guid accountId,
        IReadOnlyList<Guid> grantIds,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(grantIds, nameof(grantIds));
        var liveFromUtc = LiveReservationsFromUtc;

        return await _consumptionEventRepo.EvaluateAsync(
            (q, token) =>
                WhereCounted(
                        q.Where(c => c.AccountId == accountId && grantIds.Contains(c.GrantId)),
                        liveFromUtc
                    )
                    .GroupBy(c => c.GrantId)
                    .Select(g => new GrantTotalConsumptions(
                        g.Key,
                        g.Sum(c => (long?)c.Quantity) ?? 0L
                    ))
                    .ToListAsync(token),
            ct
        );
    }

    public async Task<List<ConsumptionTimeBucketData>> GetConsumptionTimeSeriesAsync(
        GetConsumptionTimeSeriesRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        var (accountId, fromUtc, toUtc, bucket, units, operations, providers, grantIds) = request;
        var liveFromUtc = LiveReservationsFromUtc;

        var raw = await _consumptionEventRepo.EvaluateAsync(
            (q, token) =>
            {
                q = q.Where(e =>
                    e.AccountId == accountId && e.UtcTimestamp >= fromUtc && e.UtcTimestamp <= toUtc
                );
                q = ApplyOptionalFilters(q, units, operations, providers, grantIds);
                return WhereCounted(q, liveFromUtc)
                    .Select(e => new { e.UtcTimestamp, e.Quantity })
                    .ToListAsync(token);
            },
            ct
        );

        var buckets = raw.GroupBy(e => TruncateToBucket(e.UtcTimestamp, bucket))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Quantity));

        var allBuckets = new List<ConsumptionTimeBucketData>();
        for (
            var bucketStart = TruncateToBucket(fromUtc, bucket);
            bucketStart <= toUtc;
            bucketStart = AdvanceBucket(bucketStart, bucket)
        )
        {
            allBuckets.Add(
                new ConsumptionTimeBucketData(
                    bucketStart,
                    buckets.GetValueOrDefault(bucketStart, 0L)
                )
            );
        }

        return allBuckets;
    }

    public async Task<PagedResult<ConsumptionEvent>> ListConsumptionEventsAsync(
        ListConsumptionEventsRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        if (request.Skip < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Skip must be non-negative.");
        if (request.Take <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Take must be positive.");

        var (entities, totalCount) = await _consumptionEventRepo.EvaluateAsync(
            async (q, token) =>
            {
                q = q.Where(e => e.AccountId == request.AccountId);
                q = ApplyOptionalFilters(
                    q,
                    request.Units,
                    request.Operations,
                    request.Providers,
                    request.GrantIds
                );
                if (request.FromUtc is not null)
                    q = q.Where(e => e.UtcTimestamp >= request.FromUtc);
                if (request.ToUtc is not null)
                    q = q.Where(e => e.UtcTimestamp <= request.ToUtc);

                var count = await q.CountAsync(token);
                var page = await ApplySort(q, request.SortBy, request.SortDirection)
                    .Skip(request.Skip)
                    .Take(request.Take)
                    .ToListAsync(token);

                return (page, count);
            },
            ct
        );

        return PagedResult<ConsumptionEvent>.Create(
            [.. entities.Select(e => e.ToModel())],
            totalCount,
            request.Skip,
            request.Take
        );
    }

    public async Task<List<string>> ListProvidersAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        await _consumptionEventRepo.EvaluateAsync(
            (q, token) =>
                q.Where(e => e.AccountId == accountId)
                    .Select(e => e.Provider)
                    .Distinct()
                    .OrderBy(p => p)
                    .ToListAsync(token),
            ct
        );

    public async Task<DateTime?> GetEarliestConsumptionAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        await _consumptionEventRepo.EvaluateAsync(
            (q, token) =>
                q.Where(e => e.AccountId == accountId)
                    .OrderBy(e => e.UtcTimestamp)
                    .Select(e => (DateTime?)e.UtcTimestamp)
                    .FirstOrDefaultAsync(token),
            ct
        );

    public async Task<int> CountAbandonedReservationsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var liveFromUtc = LiveReservationsFromUtc;

        return await _consumptionEventRepo.EvaluateAsync(
            (q, token) =>
                q.Where(c =>
                        c.AccountId == accountId
                        && c.FinalizedUtc == null
                        && c.UtcTimestamp < liveFromUtc
                    )
                    .CountAsync(token),
            ct
        );
    }

    private static IQueryable<ConsumptionEventEntity> ApplyOptionalFilters(
        IQueryable<ConsumptionEventEntity> q,
        IReadOnlyList<UsageUnit>? units,
        IReadOnlyList<UsageOperation>? operations,
        IReadOnlyList<string>? providers,
        IReadOnlyList<Guid>? grantIds
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
        ConsumptionEventSortField sortBy,
        SortDirection direction
    )
    {
        var descending = direction == SortDirection.Descending;
        return sortBy switch
        {
            ConsumptionEventSortField.Quantity => descending
                ? q.OrderByDescending(e => e.Quantity)
                : q.OrderBy(e => e.Quantity),
            ConsumptionEventSortField.Unit => descending
                ? q.OrderByDescending(e => e.Unit)
                : q.OrderBy(e => e.Unit),
            ConsumptionEventSortField.Operation => descending
                ? q.OrderByDescending(e => e.Operation)
                : q.OrderBy(e => e.Operation),
            ConsumptionEventSortField.Provider => descending
                ? q.OrderByDescending(e => e.Provider)
                : q.OrderBy(e => e.Provider),
            _ => descending
                ? q.OrderByDescending(e => e.UtcTimestamp)
                : q.OrderBy(e => e.UtcTimestamp),
        };
    }

    private static DateTime TruncateToBucket(DateTime dt, TimeBucket bucket) =>
        bucket switch
        {
            TimeBucket.Week => StartOfWeek(dt),
            TimeBucket.Month => new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc),
        };

    private static DateTime AdvanceBucket(DateTime dt, TimeBucket bucket) =>
        bucket switch
        {
            TimeBucket.Week => dt.AddDays(7),
            TimeBucket.Month => dt.AddMonths(1),
            _ => dt.AddDays(1),
        };

    private static DateTime StartOfWeek(DateTime dt)
    {
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-diff);
    }
}
