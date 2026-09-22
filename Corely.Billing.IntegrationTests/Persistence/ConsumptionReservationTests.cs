using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Usage;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Persistence;

/// <summary>
/// Reserve, settle, release -- and what each does to a grant's balance.
/// </summary>
/// <remarks>
/// Written against the real repo rather than a mock. The behaviour under test is arithmetic over
/// rows: which of them a balance counts, and what settling does to the quantities. A substituted
/// repo would only prove that the calls were made.
/// </remarks>
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
        // The point of reserving at all. Two extractions against the same nearly exhausted grant
        // used to both pass the check because nothing was written until the work was done.
        var writer = Ledger("job:a/step:b");

        await writer.ReserveAsync(NewEvent(quantity: 1));

        Assert.Equal(1, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_CorrectsTheQuantity_ForAReservationOfTheFloor()
    {
        // Reserve the floor, settle the truth. Nothing knows the page count until the provider has
        // processed the document, so the hold is deliberately an underestimate.
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
        // Encoding a release as Quantity = 0 would destroy what a billing dispute most wants: how
        // much was held, and for how long.
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
        // The backstop for a process killed between reserving and settling. Release carries every
        // failure the application actually sees, so this only has to catch the ones it does not.
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
        // Found by idempotency scope, so one job's settlement cannot resolve another job's hold.
        await Ledger("job:a/step:b").ReserveAsync(NewEvent(quantity: 1));
        await Ledger("job:c/step:d").ReserveAsync(NewEvent(quantity: 1));

        await Ledger("job:a/step:b")
            .SettleAsync(
                AccountId,
                TestUsage.Extraction,
                TestUsage.Page,
                new Dictionary<Guid, long> { [GrantId] = 40 }
            );

        // 40 settled for the first job, plus the second job's untouched one-page hold.
        Assert.Equal(41, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_Succeeds_ForAReplayWhoseReservationsAreAlreadyResolved()
    {
        // A dead-lettered message replayed after the first attempt settled. Not an error, and it
        // must not invent a second row.
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
        // A settled charge is final, so no TTL can ever expire it out of a balance. Only an
        // unresolved hold stops counting with age.
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
        // The overspend bug this replaces: a document reserved against a grant with one page left
        // used to be charged all five hundred pages to that grant, leaving it reading 500/1 consumed
        // next to an untouched one. Quota decides the split; this proves metering writes it.
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
        // The spillover row describes the same piece of work as the reservation it came from. Built
        // from arguments instead, it would be a row nobody could tie back to a document.
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
        // Quota can move the whole charge elsewhere -- a grant that expired between reserving and
        // settling, say. The hold on the grant it left behind is given back, not settled at zero.
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
        // Replay safety for the spillover path. The new row's idempotency key is derived exactly as
        // every other row's is, so a second settlement collides with it rather than charging again.
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
        // Nothing breaks when a hold is abandoned -- it simply stops counting against the grant.
        // That is exactly why it needs a counter: a rising number is the only sign that steps are
        // dying between reserving and settling.
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
        // Settled and released rows are resolved, however old they get. Counting by age alone would
        // report every historical charge as an abandoned hold.
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

    /// <summary>
    /// The ledger as one unit of work sees it: each call in its own DI scope, all under the same
    /// operation context.
    /// </summary>
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
