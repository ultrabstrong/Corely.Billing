using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Models;
using Corely.Billing.DataAccess;
using Corely.Billing.Grants.Models;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Common.Filtering;
using Corely.Common.Filtering.Filters;
using Corely.Common.Filtering.Ordering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Providers;

public abstract class ProviderMatrixTestsBase(ProviderTestHost host) : IAsyncLifetime
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly LedgerDriver _ledger = new(host, AccountId);

    protected ProviderTestHost Host { get; } = host;

    public async ValueTask InitializeAsync()
    {
        if (DockerAvailability.UnavailableReason is not null)
            return;

        await Host.InitializeAsync();
        await Host.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (DockerAvailability.UnavailableReason is null)
            await Host.DisposeAsync();
    }

    [RequiresDockerFact]
    public async Task MigrationsApplyCleanly()
    {
        var applied = await Host.QueryAsync(db => db.Database.GetAppliedMigrationsAsync());

        Assert.NotEmpty(applied);
    }

    [RequiresDockerFact]
    public async Task NoPendingMigrationsRemain()
    {
        var pending = await Host.QueryAsync(db => db.Database.GetPendingMigrationsAsync());

        Assert.Empty(pending);
    }

    [RequiresDockerFact]
    public async Task MigrationHistoryIsRecordedInTheBillingTable()
    {
        var count = await Host.QueryAsync(db =>
            db.Database.SqlQueryRaw<int>(
                    $"SELECT COUNT(*) AS Value FROM {MigrationConstants.DEFAULT_HISTORY_TABLE}"
                )
                .SingleAsync()
        );

        Assert.True(count > 0);
    }

    [RequiresDockerFact]
    public async Task BalanceSplitsAcrossGrants_ForWorkThatCrossesAGrantEdge()
    {
        var expiringSoon = await _ledger.SeedGrantAsync(quantity: 100, expiresInDays: 2);
        var laterGrant = await _ledger.SeedGrantAsync(quantity: 1000, expiresInDays: 60);

        await _ledger.ProcessAsync("job:a/step:1", 500);

        Assert.Equal(100, await _ledger.BalanceAsync(expiringSoon));
        Assert.Equal(400, await _ledger.BalanceAsync(laterGrant));
    }

    [RequiresDockerFact]
    public async Task UnlimitedGrantTakesTheWork_ForAnUnlimitedGrantBesideALimitedOne()
    {
        var expiringSoon = await _ledger.SeedGrantAsync(quantity: 10, expiresInDays: 2);
        var unlimited = await _ledger.SeedGrantAsync(quantity: null, expiresInDays: 365);

        await _ledger.ProcessAsync("job:a/step:1", 500);
        var availability = await Host.WithScopeAsync(services =>
            services
                .GetRequiredService<IQuotaService>()
                .GetAvailabilityAsync(AccountId, TestUsage.Extraction, TestUsage.Page)
        );

        Assert.Equal(0, await _ledger.BalanceAsync(expiringSoon));
        Assert.Equal(500, await _ledger.BalanceAsync(unlimited));
        Assert.Equal(QuotaAvailability.Available, availability);
    }

    [RequiresDockerFact]
    public async Task ExpiredHoldsStopCounting_ForAHoldPastItsTtl()
    {
        await _ledger.SeedGrantAsync(quantity: 1);
        await _ledger.ReserveAsync("job:a/step:1", quantity: 1);

        var whileHeld = await _ledger.ReserveAsync("job:b/step:1", quantity: 1);
        Host.TimeProvider.Advance(
            new ReservationOptions().ReservationTtl + TimeSpan.FromMinutes(1)
        );
        var afterExpiry = await _ledger.ReserveAsync("job:c/step:1", quantity: 1);

        Assert.Equal(ReserveQuotaResultCode.InsufficientQuotaError, whileHeld.ResultCode);
        Assert.Equal(ReserveQuotaResultCode.Success, afterExpiry.ResultCode);
    }

    [RequiresDockerFact]
    public async Task ReplayChargesOnce_ForAStepReplayedUnderTheSameScope()
    {
        var grant = await _ledger.SeedGrantAsync(quantity: 100);

        await _ledger.ProcessAsync("job:a/step:1", 40);
        await _ledger.ProcessAsync("job:a/step:1", 40);

        Assert.Equal(40, await _ledger.BalanceAsync(grant));
    }

    [RequiresDockerFact]
    public async Task UniqueIndexRejectsADuplicateIdempotencyKey()
    {
        var grant = await _ledger.SeedGrantAsync(quantity: 100);
        await _ledger.ReserveAsync("job:a/step:1", quantity: 1);

        var exception = await Record.ExceptionAsync(() =>
            Host.QueryAsync(async db =>
            {
                var existing = await db.ConsumptionEvents.AsNoTracking().SingleAsync();
                existing.ConsumptionId = Guid.CreateVersion7();
                db.ConsumptionEvents.Add(existing);
                return await db.SaveChangesAsync();
            })
        );

        Assert.IsType<DbUpdateException>(exception);
        Assert.Equal(1, await _ledger.BalanceAsync(grant));
    }

    [RequiresDockerFact]
    public async Task UniqueIndexAllowsTheSameKey_ForDifferentAccounts()
    {
        var inserted = await Host.QueryAsync(db =>
        {
            db.ConsumptionEvents.AddRange(
                NewConsumption(Guid.CreateVersion7(), "job:a/step:1|extraction|page|g"),
                NewConsumption(Guid.CreateVersion7(), "job:a/step:1|extraction|page|g")
            );
            return db.SaveChangesAsync();
        });

        Assert.Equal(2, inserted);
    }

    [RequiresDockerFact]
    public async Task CorrelationIndexAllowsRepeats_ForOneUnitOfWorkSpanningGrants()
    {
        var correlationId = Guid.CreateVersion7();
        var first = NewConsumption(AccountId, "job:a/step:1|extraction|page|g1");
        var second = NewConsumption(AccountId, "job:a/step:1|extraction|page|g2");
        first.CorrelationId = correlationId;
        second.CorrelationId = correlationId;

        var inserted = await Host.QueryAsync(db =>
        {
            db.ConsumptionEvents.AddRange(first, second);
            return db.SaveChangesAsync();
        });

        Assert.Equal(2, inserted);
    }

    private static ConsumptionEventEntity NewConsumption(Guid accountId, string idempotencyKey) =>
        new()
        {
            ConsumptionId = Guid.CreateVersion7(),
            AccountId = accountId,
            GrantId = Guid.CreateVersion7(),
            CorrelationId = Guid.CreateVersion7(),
            IdempotencyKey = idempotencyKey,
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = 1,
            Provider = "prov",
            UtcTimestamp = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc),
        };

    [RequiresDockerFact]
    public async Task TimeSeriesBucketsTranslate_ForDailyBuckets()
    {
        await _ledger.SeedGrantAsync(quantity: 1000);
        await _ledger.ProcessAsync("job:a/step:1", 10);
        Host.TimeProvider.Advance(TimeSpan.FromDays(1));
        await _ledger.ProcessAsync("job:b/step:1", 20);

        var now = Host.TimeProvider.GetUtcNow().UtcDateTime;
        var result = await Host.WithScopeAsync(services =>
            services
                .GetRequiredService<IConsumptionService>()
                .GetConsumptionTimeSeriesAsync(
                    new GetConsumptionTimeSeriesRequest(
                        AccountId,
                        now.AddDays(-3),
                        now.AddDays(1),
                        TimeBucket.Day
                    )
                )
        );

        Assert.Equal(30, result.Item!.Sum(b => b.TotalQuantity));
        Assert.Equal(2, result.Item.Count(b => b.TotalQuantity > 0));
    }

    [RequiresDockerFact]
    public async Task ConsumptionListingTranslates_ForFiltersSortAndPaging()
    {
        await _ledger.SeedGrantAsync(quantity: 1000);
        await _ledger.ProcessAsync("job:a/step:1", 10);
        await _ledger.ProcessAsync("job:b/step:1", 20);
        await _ledger.ProcessAsync("job:c/step:1", 30);

        var result = await Host.WithScopeAsync(services =>
            services
                .GetRequiredService<IConsumptionService>()
                .ListConsumptionEventsAsync(
                    new ListConsumptionEventsRequest(
                        AccountId,
                        Units: [TestUsage.Page],
                        Operations: [TestUsage.Extraction],
                        Providers: ["prov"],
                        SortBy: ConsumptionEventSortField.Quantity,
                        SortDirection: SortDirection.Ascending,
                        Skip: 1,
                        Take: 1
                    )
                )
        );

        Assert.Equal(3, result.Data!.TotalCount);
        Assert.Equal(20, Assert.Single(result.Data.Items).Quantity);
    }

    [RequiresDockerFact]
    public async Task GrantListingTranslates_ForAFilterAndOrder()
    {
        await _ledger.SeedGrantAsync(quantity: 10);
        await _ledger.SeedGrantAsync(quantity: 30);
        await _ledger.SeedGrantAsync(quantity: null);
        await _ledger.SeedGrantAsync(quantity: 20);

        var result = await Host.WithScopeAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .ListGrantsAsync(
                    new ListGrantsRequest(
                        AccountId,
                        Filter: Filter
                            .For<Grant>()
                            .Where(g => g.Quantity, ComparableFilter<long>.IsNotNull()),
                        Order: Order.For<Grant>().By(g => g.Quantity, SortDirection.Ascending)
                    )
                )
        );

        Assert.Equal([10, 20, 30], result.Data!.Items.Select(g => g.Quantity));
    }
}

public class MsSqlProviderMatrixTests()
    : ProviderMatrixTestsBase(new ProviderTestHost(DatabaseProvider.MsSql)) { }

public class MySqlProviderMatrixTests()
    : ProviderMatrixTestsBase(new ProviderTestHost(DatabaseProvider.MySql)) { }
