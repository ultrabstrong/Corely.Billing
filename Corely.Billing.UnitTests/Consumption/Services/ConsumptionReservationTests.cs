using Corely.Billing;
using Corely.Billing.Consumption.DataAccess;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Services;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Corely.Billing.UnitTests.Consumption.Services;

/// <summary>
/// Reserve, settle, release -- and what each does to a grant's balance.
/// </summary>
/// <remarks>
/// Written against the real repo rather than a mock. The behaviour under test is arithmetic over
/// rows: which of them a balance counts, and what settling does to the quantities. A substituted
/// repo would only prove that the calls were made.
/// </remarks>
public class ConsumptionReservationTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SecondGrantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly IRepo<ConsumptionEventEntity> _repo = ServiceFixture.GetRequiredService<
        IRepo<ConsumptionEventEntity>
    >();
    private readonly MeteringOptions _options = new();

    [Fact]
    public async Task ReserveAsync_CountsAgainstTheGrant_ForAnOutstandingReservation()
    {
        // The point of reserving at all. Two extractions against the same nearly exhausted grant
        // used to both pass the check because nothing was written until the work was done.
        var writer = NewWriter("job:a/step:b");

        await writer.ReserveAsync(NewEvent(quantity: 1));

        Assert.Equal(1, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_CorrectsTheQuantity_ForAReservationOfTheFloor()
    {
        // Reserve the floor, settle the truth. Nothing knows the page count until the provider has
        // processed the document, so the hold is deliberately an underestimate.
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 1));

        var result = await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 12 }
        );

        Assert.Equal(SettleConsumptionResultCode.Success, result.ResultCode);
        Assert.Equal(12, result.SettledQuantity);
        Assert.Equal(12, await BalanceAsync());
    }

    [Fact]
    public async Task ReleaseAsync_StopsCountingAgainstTheGrant_ForAFailedStep()
    {
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        await writer.ReleaseAsync(AccountId, TestUsage.Extraction, TestUsage.Page);

        Assert.Equal(0, await BalanceAsync());
    }

    [Fact]
    public async Task ReleaseAsync_KeepsTheOriginalQuantity_ForAReleasedReservation()
    {
        // Encoding a release as Quantity = 0 would destroy what a billing dispute most wants: how
        // much was held, and for how long.
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        await writer.ReleaseAsync(AccountId, TestUsage.Extraction, TestUsage.Page);

        var row = await _repo.GetAsync(e => e.AccountId == AccountId);
        Assert.Multiple(() =>
        {
            Assert.Equal(5, row!.Quantity);
            Assert.Equal(ConsumptionOutcome.Released, row.Outcome);
            Assert.Equal(Now.UtcDateTime, row.FinalizedUtc);
        });
    }

    [Fact]
    public async Task Balance_StopsCountingTheReservation_ForAHoldPastItsTtl()
    {
        // The backstop for a process killed between reserving and settling. Release carries every
        // failure the application actually sees, so this only has to catch the ones it does not.
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        _time.Advance(_options.ReservationTtl + TimeSpan.FromMinutes(1));

        Assert.Equal(0, await BalanceAsync());
    }

    [Fact]
    public async Task Balance_KeepsCountingTheReservation_ForAHoldInsideItsTtl()
    {
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        _time.Advance(_options.ReservationTtl - TimeSpan.FromMinutes(1));

        Assert.Equal(5, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_ResolvesOnlyItsOwnReservation_ForConcurrentOperations()
    {
        // Found by idempotency scope, so one job's settlement cannot resolve another job's hold.
        await NewWriter("job:a/step:b").ReserveAsync(NewEvent(quantity: 1));
        await NewWriter("job:c/step:d").ReserveAsync(NewEvent(quantity: 1));

        await NewWriter("job:a/step:b")
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
        var writer = NewWriter("job:a/step:b");
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

        Assert.Equal(SettleConsumptionResultCode.Success, second.ResultCode);
        Assert.Equal(7, await BalanceAsync());
    }

    [Fact]
    public async Task SaveAsync_CountsAgainstTheGrantForever_ForAChargeThatWasNeverReserved()
    {
        // A directly saved charge is settled on arrival, so no TTL can ever expire it out of a
        // balance. That is what the migration's backfill guarantees for every historical row.
        var writer = NewWriter("job:a/step:b");
        await writer.SaveAsync(NewEvent(quantity: 3));

        _time.Advance(_options.ReservationTtl * 10);

        Assert.Equal(3, await BalanceAsync());
    }

    [Fact]
    public async Task SettleAsync_ChargesTheSecondGrant_ForWorkThatOutgrewTheFirst()
    {
        // The overspend bug this replaces: a document reserved against a grant with one page left
        // used to be charged all five hundred pages to that grant, leaving it reading 500/1 consumed
        // next to an untouched one. Quota decides the split; this proves metering writes it.
        var writer = NewWriter("job:a/step:b");
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
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 1));

        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 1, [SecondGrantId] = 4 }
        );

        var spillover = await _repo.GetAsync(e => e.GrantId == SecondGrantId);
        var reserved = await _repo.GetAsync(e => e.GrantId == GrantId);

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
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 3));

        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [SecondGrantId] = 3 }
        );

        var released = await _repo.GetAsync(e => e.GrantId == GrantId);

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
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 1));

        var split = new Dictionary<Guid, long> { [GrantId] = 1, [SecondGrantId] = 9 };
        await writer.SettleAsync(AccountId, TestUsage.Extraction, TestUsage.Page, split);
        await writer.SettleAsync(AccountId, TestUsage.Extraction, TestUsage.Page, split);

        Assert.Equal(9, await BalanceAsync(SecondGrantId));
    }

    private async Task<long> BalanceAsync(Guid? grantId = null)
    {
        var grant = grantId ?? GrantId;
        var reader = new ConsumptionReader(
            _repo,
            Mock.Of<IOperationContextAccessor>(),
            Options.Create(_options),
            _time
        );
        var result = await reader.GetGrantConsumptionTotalsAsync(AccountId, [grant]);
        return result.Totals?.FirstOrDefault(t => t.GrantId == grant)?.TotalConsumedQuantity ?? 0L;
    }

    private ConsumptionWriter NewWriter(string idempotencyScope)
    {
        var accessor = new Mock<IOperationContextAccessor>();
        accessor
            .SetupGet(a => a.Current)
            .Returns(new OperationContext(Guid.CreateVersion7(), idempotencyScope));

        return new ConsumptionWriter(
            _repo,
            accessor.Object,
            TestUsage.Vocabulary,
            _time,
            NullLogger<ConsumptionWriter>.Instance
        );
    }

    private ConsumptionEvent NewEvent(long quantity)
    {
        var result = ConsumptionEvent.Create(
            accountId: AccountId,
            quantity: quantity,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            provider: "prov",
            utcTimestamp: _time.GetUtcNow().UtcDateTime,
            grantId: GrantId
        );
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public async Task CountAbandonedReservationsAsync_CountsTheHold_ForOneThatOutlivedItsTtl()
    {
        // Nothing breaks when a hold is abandoned -- it simply stops counting against the grant.
        // That is exactly why it needs a counter: a rising number is the only sign that steps are
        // dying between reserving and settling.
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        _time.Advance(_options.ReservationTtl + TimeSpan.FromMinutes(1));

        Assert.Equal(1, await AbandonedAsync());
    }

    [Fact]
    public async Task CountAbandonedReservationsAsync_CountsNothing_ForAHoldInsideItsTtl()
    {
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));

        Assert.Equal(0, await AbandonedAsync());
    }

    [Fact]
    public async Task CountAbandonedReservationsAsync_CountsNothing_ForAHoldThatWasResolved()
    {
        // Settled and released rows are resolved, however old they get. Counting by age alone would
        // report every historical charge as an abandoned hold.
        var writer = NewWriter("job:a/step:b");
        await writer.ReserveAsync(NewEvent(quantity: 5));
        await writer.SettleAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            new Dictionary<Guid, long> { [GrantId] = 5 }
        );

        _time.Advance(_options.ReservationTtl * 10);

        Assert.Equal(0, await AbandonedAsync());
    }

    private async Task<int> AbandonedAsync()
    {
        var reader = new ConsumptionReader(
            _repo,
            Mock.Of<IOperationContextAccessor>(),
            Options.Create(_options),
            _time
        );
        return await reader.CountAbandonedReservationsAsync(AccountId);
    }
}
