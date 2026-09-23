using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Usage;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Persistence;

public sealed class ConsumptionReservationTests : IDisposable
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SecondGrantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly BillingTestHost _host = new();
    private readonly ReservationOptions _options = new();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task ReserveAsync_CountsAgainstTheGrant_ForAnOutstandingReservation()
    {
        var writer = Ledger("job:a/step:b");

        await writer.ReserveAsync(NewEvent(quantity: 1));

        Assert.Equal(1, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_CorrectsTheQuantity_ForAReservationOfTheFloor()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 1));

        var result = await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 12 }
        );

        Assert.Equal(ResolveConsumptionResultCode.Success, result.ResultCode);
        Assert.Equal(12, result.SettledQuantity);
        Assert.Equal(12, await BalanceAsync());
    }

    [Fact]
    public async Task ReleaseAsync_StopsCountingAgainstTheGrant_ForAFailedStep()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        await writer.ReleaseAsync(AccountId, TestUsage.Extraction, TestUsage.Page);

        Assert.Equal(0, await BalanceAsync());
    }

    [Fact]
    public async Task ReleaseAsync_KeepsTheOriginalQuantity_ForAReleasedReservation()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        await writer.ReleaseAsync(AccountId, TestUsage.Extraction, TestUsage.Page);

        var row = await RowAsync(e => e.AccountId == AccountId);
        Assert.Multiple(() =>
        {
            Assert.Equal(5, row!.Quantity);
            Assert.Equal(ConsumptionOutcome.Released, row.Outcome);
            Assert.Equal(_host.TimeProvider.GetUtcNow().UtcDateTime, row.FinalizedUtc);
        });
    }

    [Fact]
    public async Task Balance_StopsCountingTheReservation_ForAHoldPastItsTtl()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        _host.TimeProvider.Advance(_options.ReservationTtl + TimeSpan.FromMinutes(1));

        Assert.Equal(0, await BalanceAsync());
    }

    [Fact]
    public async Task Balance_KeepsCountingTheReservation_ForAHoldInsideItsTtl()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        _host.TimeProvider.Advance(_options.ReservationTtl - TimeSpan.FromMinutes(1));

        Assert.Equal(5, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_ResolvesOnlyItsOwnReservation_ForConcurrentOperations()
    {
        await Ledger("job:a/step:b").ReserveAsync(NewEvent(quantity: 1));
        await Ledger("job:c/step:d").ReserveAsync(NewEvent(quantity: 1));

        await Ledger("job:a/step:b")
            .SettleAsync(
                AccountId,
                TestUsage.Extraction,
                TestUsage.Page,
                new Dictionary<Guid, long> { [GrantId] = 40 }
            );

        Assert.Equal(41, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_Succeeds_ForAReplayWhoseReservationsAreAlreadyResolved()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 1));
        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 7 }
        );

        var second = await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 7 }
        );

        Assert.Equal(ResolveConsumptionResultCode.Success, second.ResultCode);
        Assert.Equal(7, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_CountsAgainstTheGrantForever_ForASettledCharge()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 3));
        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 3 }
        );

        _host.TimeProvider.Advance(_options.ReservationTtl * 10);

        Assert.Equal(3, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_ChargesTheSecondGrant_ForWorkThatOutgrewTheFirst()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 1));

        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 1, [SecondGrantId] = 99 }
        );

        Assert.Equal(1, await BalanceAsync());
        Assert.Equal(99, await BalanceAsync(SecondGrantId));
    }

    [Fact]
    public async Task SettleAsync_CopiesTheProviderAndTags_ForAGrantItNeverReserved()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 1));

        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 1, [SecondGrantId] = 4 }
        );

        var spillover = await RowAsync(e => e.GrantId == SecondGrantId);
        var reserved = await RowAsync(e => e.GrantId == GrantId);

        Assert.Multiple(() =>
        {
            Assert.Equal(reserved!.Provider, spillover!.Provider);
            Assert.Equal(reserved.CorrelationId, spillover.CorrelationId);
            Assert.Equal(ConsumptionOutcome.Settled, spillover.Outcome);
        });
    }

    [Fact]
    public async Task SettleAsync_ReleasesTheReservation_ForAGrantAbsentFromTheSplit()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 3));

        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [SecondGrantId] = 3 }
        );

        var released = await RowAsync(e => e.GrantId == GrantId);

        Assert.Multiple(() =>
        {
            Assert.Equal(ConsumptionOutcome.Released, released!.Outcome);
            Assert.Equal(3, released.Quantity);
        });
        Assert.Equal(0, await BalanceAsync());
        Assert.Equal(3, await BalanceAsync(SecondGrantId));
    }

    [Fact]
    public async Task SettleAsync_WritesTheSameSpilloverRow_ForARepeatedSettlement()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 1));

        var split = new Dictionary<Guid, long> { [GrantId] = 1, [SecondGrantId] = 9 };
        await writer.SettleAsync(AccountId, TestUsage.Extraction, TestUsage.Page, split);
        await writer.SettleAsync(AccountId, TestUsage.Extraction, TestUsage.Page, split);

        Assert.Equal(9, await BalanceAsync(SecondGrantId));
    }

    [Fact]
    public async Task ListOutstandingReservationsAsync_ReturnsOnlyThisOperationsOpenHolds_ForMixedRows()
    {
        var mine = Ledger("job:a/step:b");
        await mine.ReserveAsync(NewEvent(quantity: 2));
        await Ledger("job:c/step:d").ReserveAsync(NewEvent(quantity: 5));

        var outstanding = await mine.ListOutstandingReservationsAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page
        );

        Assert.Equal(2, Assert.Single(outstanding!).Quantity);
    }

    [Fact]
    public async Task ListOutstandingReservationsAsync_ReturnsNothing_ForASettledOperation()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 2));
        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 2 }
        );

        var outstanding = await writer.ListOutstandingReservationsAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page
        );

        Assert.Empty(outstanding!);
    }

    private async Task<long> BalanceAsync(Guid? grantId = null)
    {
        var grant = grantId ?? GrantId;
        var totals = await _host.WithScopeAsync(services =>
            services
                .GetRequiredService<IConsumptionReportProcessor>()
                .GetGrantConsumptionTotalsAsync(AccountId, [grant])
        );
        return totals.FirstOrDefault(t => t.GrantId == grant)?.TotalConsumedQuantity ?? 0L;
    }

    private Task<ConsumptionEventEntity?> RowAsync(
        System.Linq.Expressions.Expression<Func<ConsumptionEventEntity, bool>> query
    ) =>
        _host.WithScopeAsync(services =>
            services.GetRequiredService<IRepo<ConsumptionEventEntity>>().GetAsync(query)
        );

    private ScopedLedger Ledger(string idempotencyScope) => new(_host, idempotencyScope);

    private ConsumptionEvent NewEvent(long quantity) =>
        new()
        {
            AccountId = AccountId,
            GrantId = GrantId,
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = quantity,
            Provider = "prov",
            UtcTimestamp = _host.TimeProvider.GetUtcNow().UtcDateTime,
        };

    [Fact]
    public async Task CountAbandonedReservationsAsync_CountsTheHold_ForOneThatOutlivedItsTtl()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        _host.TimeProvider.Advance(_options.ReservationTtl + TimeSpan.FromMinutes(1));

        Assert.Equal(1, await AbandonedAsync());
    }

    [Fact]
    public async Task CountAbandonedReservationsAsync_CountsNothing_ForAHoldInsideItsTtl()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        Assert.Equal(0, await AbandonedAsync());
    }

    [Fact]
    public async Task CountAbandonedReservationsAsync_CountsNothing_ForAHoldThatWasResolved()
    {
        var writer = Ledger("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));
        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 5 }
        );

        _host.TimeProvider.Advance(_options.ReservationTtl * 10);

        Assert.Equal(0, await AbandonedAsync());
    }

    private Task<int> AbandonedAsync() =>
        _host.WithScopeAsync(services =>
            services
                .GetRequiredService<IConsumptionReportProcessor>()
                .CountAbandonedReservationsAsync(AccountId)
        );

    private sealed class ScopedLedger(BillingTestHost host, string idempotencyScope)
    {
        public Task<ReserveConsumptionResult> ReserveAsync(ConsumptionEvent consumptionEvent) =>
            host.InOperationAsync(
                idempotencyScope,
                services =>
                    services
                        .GetRequiredService<IConsumptionProcessor>()
                        .ReserveAsync(consumptionEvent)
            );

        public Task<ResolveConsumptionResult> SettleAsync(
            Guid accountId,
            UsageOperation operation,
            UsageUnit unit,
            IReadOnlyDictionary<Guid, long> quantityByGrant
        ) =>
            host.InOperationAsync(
                idempotencyScope,
                services =>
                    services
                        .GetRequiredService<IConsumptionProcessor>()
                        .SettleAsync(accountId, operation, unit, quantityByGrant)
            );

        public Task<ResolveConsumptionResult> ReleaseAsync(
            Guid accountId,
            UsageOperation operation,
            UsageUnit unit
        ) =>
            host.InOperationAsync(
                idempotencyScope,
                services =>
                    services
                        .GetRequiredService<IConsumptionProcessor>()
                        .ReleaseAsync(accountId, operation, unit)
            );

        public Task<List<ConsumptionEvent>?> ListOutstandingReservationsAsync(
            Guid accountId,
            UsageOperation operation,
            UsageUnit unit
        ) =>
            host.InOperationAsync(
                idempotencyScope,
                services =>
                    services
                        .GetRequiredService<IConsumptionProcessor>()
                        .ListOutstandingReservationsAsync(accountId, operation, unit)
            );
    }
}
