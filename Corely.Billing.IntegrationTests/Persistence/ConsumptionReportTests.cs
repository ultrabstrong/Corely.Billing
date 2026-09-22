using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Common.Filtering.Ordering;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Persistence;

/// <summary>
/// Reading and aggregating the consumption ledger.
/// </summary>
/// <remarks>
/// <para>
/// Every seeded row carries a distinct <c>IdempotencyKey</c> even though nothing here reads it.
/// <c>(AccountId, IdempotencyKey)</c> is unique and most of these fixtures put several rows under one
/// account, so a shared or blank key is rejected by the provider before any assertion runs.
/// </para>
/// <para>
/// They are also seeded as settled, which is what they are: charges that already happened. An
/// unresolved row is a reservation, and reporting stops counting one once it is past its TTL --
/// so leaving these unresolved would quietly drop every fixture out of every total.
/// </para>
/// </remarks>
public sealed class ConsumptionReportTests : IDisposable
{
    private readonly BillingTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static readonly Guid AccountId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AccountId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AccountId3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid NonExistentAccountId = Guid.Parse(
        "99999999-9999-9999-9999-999999999999"
    );
    private static readonly DateTime SettledUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetConsumptionTotalAsync_Sums_Over_Range_With_Filters()
    {
        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Quantity = 2,
                Provider = "prov",
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                Quantity = 3,
                Provider = "prov",
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId3,
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                Unit = TestUsage.Page,
                Operation = TestUsage.Other,
                UtcTimestamp = new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                Quantity = 5,
                Provider = "other",
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                Quantity = 10,
                Provider = "prov",
            },
        };

        await SeedAsync(data);

        var sum = await ReportAsync(r =>
            r.GetConsumptionTotalAsync(
                new GetConsumptionTotalRequest(
                    AccountId2,
                    TestUsage.Extraction,
                    TestUsage.Page,
                    FromUtc: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    ToUtc: new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc),
                    Provider: "prov"
                )
            )
        );

        var expectedSum = data.Where(e =>
                e.AccountId == AccountId2
                && e.Unit == TestUsage.Page
                && e.Operation == TestUsage.Extraction
                && e.UtcTimestamp >= new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                && e.UtcTimestamp <= new DateTime(2025, 1, 2, 12, 0, 0, DateTimeKind.Utc)
                && e.Provider == "prov"
            )
            .Sum(e => e.Quantity);

        Assert.Equal(expectedSum, sum);
    }

    [Fact]
    public async Task GetConsumptionTotalAsync_Returns_GrandTotal_When_No_FromUtc()
    {
        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = DateTime.UtcNow.AddDays(-10),
                Quantity = 2,
                Provider = "prov",
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = DateTime.UtcNow.AddDays(-5),
                Quantity = 3,
                Provider = "prov",
            },
        };

        await SeedAsync(data);

        var sum = await ReportAsync(r =>
            r.GetConsumptionTotalAsync(
                new GetConsumptionTotalRequest(AccountId1, TestUsage.Extraction, TestUsage.Page)
            )
        );

        var expectedSum = data.Sum(e => e.Quantity);
        Assert.Equal(expectedSum, sum);
    }

    [Fact]
    public async Task GetConsumptionTotalAsync_Returns_Zero_When_No_Matches()
    {
        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = DateTime.UtcNow,
                Quantity = 2,
                Provider = "prov",
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = DateTime.UtcNow,
                Quantity = 3,
                Provider = "prov",
            },
        };

        await SeedAsync(data);

        var sum = await ReportAsync(r =>
            r.GetConsumptionTotalAsync(
                new GetConsumptionTotalRequest(
                    NonExistentAccountId,
                    TestUsage.Extraction,
                    TestUsage.Page
                )
            )
        );

        Assert.Equal(0, sum);
    }

    [Fact]
    public async Task GetConsumptionTimeSeriesAsync_GroupsByDay_ForDailyBucket()
    {
        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 3, 1, 10, 0, 0, DateTimeKind.Utc),
                Quantity = 5,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 3, 1, 14, 0, 0, DateTimeKind.Utc),
                Quantity = 3,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 3, 2, 8, 0, 0, DateTimeKind.Utc),
                Quantity = 7,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
        };

        await SeedAsync(data);

        var result = await ReportAsync(r =>
            r.GetConsumptionTimeSeriesAsync(
                new GetConsumptionTimeSeriesRequest(
                    AccountId1,
                    new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2025, 3, 2, 23, 59, 59, DateTimeKind.Utc),
                    TimeBucket.Day
                )
            )
        );

        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), result[0].BucketStart);
        Assert.Equal(8, result[0].TotalQuantity);
        Assert.Equal(new DateTime(2025, 3, 2, 0, 0, 0, DateTimeKind.Utc), result[1].BucketStart);
        Assert.Equal(7, result[1].TotalQuantity);
    }

    [Fact]
    public async Task GetConsumptionTimeSeriesAsync_GroupsByMonth_ForMonthlyBucket()
    {
        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                Quantity = 10,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                Quantity = 20,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
        };

        await SeedAsync(data);

        var result = await ReportAsync(r =>
            r.GetConsumptionTimeSeriesAsync(
                new GetConsumptionTimeSeriesRequest(
                    AccountId1,
                    new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2025, 3, 31, 23, 59, 59, DateTimeKind.Utc),
                    TimeBucket.Month
                )
            )
        );

        Assert.Equal(3, result.Count);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), result[0].BucketStart);
        Assert.Equal(10, result[0].TotalQuantity);
        Assert.Equal(new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc), result[1].BucketStart);
        Assert.Equal(0, result[1].TotalQuantity);
        Assert.Equal(new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc), result[2].BucketStart);
        Assert.Equal(20, result[2].TotalQuantity);
    }

    [Fact]
    public async Task GetConsumptionTimeSeriesAsync_FillsEmptyBuckets_ForGapsInData()
    {
        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc),
                Quantity = 4,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 3, 5, 9, 0, 0, DateTimeKind.Utc),
                Quantity = 6,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
        };

        await SeedAsync(data);

        var result = await ReportAsync(r =>
            r.GetConsumptionTimeSeriesAsync(
                new GetConsumptionTimeSeriesRequest(
                    AccountId1,
                    new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2025, 3, 5, 23, 59, 59, DateTimeKind.Utc),
                    TimeBucket.Day
                )
            )
        );

        Assert.Equal(5, result.Count);
        Assert.Equal(4, result[0].TotalQuantity); // Mar 1
        Assert.Equal(0, result[1].TotalQuantity); // Mar 2
        Assert.Equal(0, result[2].TotalQuantity); // Mar 3
        Assert.Equal(0, result[3].TotalQuantity); // Mar 4
        Assert.Equal(6, result[4].TotalQuantity); // Mar 5
    }

    [Fact]
    public async Task GetConsumptionTimeSeriesAsync_FiltersCorrectly_ForUnitAndOperation()
    {
        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 4, 1, 10, 0, 0, DateTimeKind.Utc),
                Quantity = 5,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                Unit = TestUsage.Page,
                Operation = TestUsage.Other,
                UtcTimestamp = new DateTime(2025, 4, 1, 11, 0, 0, DateTimeKind.Utc),
                Quantity = 10,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                Unit = TestUsage.Document,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc),
                Quantity = 15,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
        };

        await SeedAsync(data);

        var result = await ReportAsync(r =>
            r.GetConsumptionTimeSeriesAsync(
                new GetConsumptionTimeSeriesRequest(
                    AccountId2,
                    new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2025, 4, 1, 23, 59, 59, DateTimeKind.Utc),
                    TimeBucket.Day,
                    Units: [TestUsage.Page],
                    Operations: [TestUsage.Extraction]
                )
            )
        );

        Assert.Single(result);
        Assert.Equal(5, result[0].TotalQuantity);
    }

    [Fact]
    public async Task GetConsumptionTimeSeriesAsync_ReturnsEmptyBuckets_ForNoMatchingData()
    {
        var result = await ReportAsync(r =>
            r.GetConsumptionTimeSeriesAsync(
                new GetConsumptionTimeSeriesRequest(
                    NonExistentAccountId,
                    new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2025, 3, 3, 23, 59, 59, DateTimeKind.Utc),
                    TimeBucket.Day
                )
            )
        );

        Assert.Equal(3, result.Count);
        Assert.All(result, b => Assert.Equal(0, b.TotalQuantity));
    }

    [Fact]
    public async Task ListConsumptionEventsAsync_ReturnsPaged_ForSkipAndTake()
    {
        var data = Enumerable
            .Range(0, 10)
            .Select(i => new ConsumptionEventEntity
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId3,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(i),
                Quantity = i + 1,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = $"seed/{i}",
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            })
            .ToList();

        await SeedAsync(data);

        var result = await ReportAsync(r =>
            r.ListConsumptionEventsAsync(
                new ListConsumptionEventsRequest(AccountId3, Skip: 2, Take: 3)
            )
        );

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(10, result.TotalCount);
    }

    [Fact]
    public async Task ListConsumptionEventsAsync_SortsNewestFirst_ForDefaultSort()
    {
        var ts1 = new DateTime(2025, 6, 3, 0, 0, 0, DateTimeKind.Utc);
        var ts2 = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var ts3 = new DateTime(2025, 6, 2, 0, 0, 0, DateTimeKind.Utc);

        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = ts1,
                Quantity = 1,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = ts2,
                Quantity = 2,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = ts3,
                Quantity = 3,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
        };

        await SeedAsync(data);

        var result = await ReportAsync(r =>
            r.ListConsumptionEventsAsync(new ListConsumptionEventsRequest(AccountId1))
        );

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(ts1, result.Items[0].UtcTimestamp);
        Assert.Equal(ts3, result.Items[1].UtcTimestamp);
        Assert.Equal(ts2, result.Items[2].UtcTimestamp);
    }

    [Fact]
    public async Task ListConsumptionEventsAsync_SortsOldestFirst_ForAscendingDirection()
    {
        var ts1 = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var ts2 = new DateTime(2025, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var ts3 = new DateTime(2025, 7, 3, 0, 0, 0, DateTimeKind.Utc);

        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = ts1,
                Quantity = 1,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = ts2,
                Quantity = 2,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = ts3,
                Quantity = 3,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
        };

        await SeedAsync(data);

        var result = await ReportAsync(r =>
            r.ListConsumptionEventsAsync(
                new ListConsumptionEventsRequest(AccountId2, SortDirection: SortDirection.Ascending)
            )
        );

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(ts1, result.Items[0].UtcTimestamp);
        Assert.Equal(ts2, result.Items[1].UtcTimestamp);
        Assert.Equal(ts3, result.Items[2].UtcTimestamp);
    }

    [Fact]
    public async Task ListConsumptionEventsAsync_FiltersCorrectly_ForDateRange()
    {
        var data = new List<ConsumptionEventEntity>
        {
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId3,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                Quantity = 1,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId3,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 8, 10, 0, 0, 0, DateTimeKind.Utc),
                Quantity = 2,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = AccountId3,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                UtcTimestamp = new DateTime(2025, 8, 20, 0, 0, 0, DateTimeKind.Utc),
                Quantity = 3,
                Provider = "mistral",
                CorrelationId = Guid.CreateVersion7(),
                IdempotencyKey = Guid.CreateVersion7().ToString(),
                FinalizedUtc = SettledUtc,
                Outcome = ConsumptionOutcome.Settled,
                GrantId = Guid.CreateVersion7(),
            },
        };

        await SeedAsync(data);

        var result = await ReportAsync(r =>
            r.ListConsumptionEventsAsync(
                new ListConsumptionEventsRequest(
                    AccountId3,
                    FromUtc: new DateTime(2025, 8, 5, 0, 0, 0, DateTimeKind.Utc),
                    ToUtc: new DateTime(2025, 8, 15, 0, 0, 0, DateTimeKind.Utc)
                )
            )
        );

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(2, result.Items[0].Quantity);
    }

    [Fact]
    public async Task ListConsumptionEventsAsync_ReturnsEmpty_ForNoMatchingAccount()
    {
        var result = await ReportAsync(r =>
            r.ListConsumptionEventsAsync(new ListConsumptionEventsRequest(NonExistentAccountId))
        );

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    private Task SeedAsync(IEnumerable<ConsumptionEventEntity> rows) =>
        _host.WithScopeAsync(async services =>
        {
            await services.GetRequiredService<IRepo<ConsumptionEventEntity>>().CreateAsync(rows);
            return 0;
        });

    private Task<T> ReportAsync<T>(Func<IConsumptionReportProcessor, Task<T>> call) =>
        _host.WithScopeAsync(services =>
            call(services.GetRequiredService<IConsumptionReportProcessor>())
        );
}
