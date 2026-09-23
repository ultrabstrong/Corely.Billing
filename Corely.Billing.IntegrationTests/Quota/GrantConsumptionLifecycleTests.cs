using Corely.Billing.Consumption.Models;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.IntegrationTests.Quota;

/// <summary>
/// Grants and the consumption ledger together, asking what each grant's balance reads afterwards.
/// </summary>
/// <remarks>
/// Every layer below has its own tests, and a grant with one unit left was still once charged five
/// hundred: the policy tests asserted an allocation, the quota tests a call to a substituted ledger,
/// and the ledger tests rows. None closed the loop from a grant, through a charge, back to a balance.
/// These do, through the public services, with a fake clock for validity windows and the TTL.
/// </remarks>
public sealed class GrantConsumptionLifecycleTests : IDisposable
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly BillingTestHost _host = new();
    private readonly LedgerDriver _ledger;

    public GrantConsumptionLifecycleTests()
    {
        _ledger = new LedgerDriver(_host, AccountId);
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task Balance_ChargesOneGrant_ForWorkThatFitsInsideIt()
    {
        var grant = await _ledger.SeedGrantAsync(quantity: 100);

        await _ledger.ProcessAsync("job:a/step:1", 40);

        Assert.Equal(40, await _ledger.BalanceAsync(grant));
    }

    [Fact]
    public async Task Balance_SplitsAcrossBothGrants_ForWorkThatCrossesAGrantEdge()
    {
        var expiringSoon = await _ledger.SeedGrantAsync(quantity: 100, expiresInDays: 2);
        var laterGrant = await _ledger.SeedGrantAsync(quantity: 1000, expiresInDays: 60);

        await _ledger.ProcessAsync("job:a/step:1", 500);

        Assert.Equal(100, await _ledger.BalanceAsync(expiringSoon));
        Assert.Equal(400, await _ledger.BalanceAsync(laterGrant));
    }

    [Fact]
    public async Task Balance_LeavesTheNextGrantUntouched_ForWorkThatExactlyExhaustsTheFirst()
    {
        var expiringSoon = await _ledger.SeedGrantAsync(quantity: 100, expiresInDays: 2);
        var laterGrant = await _ledger.SeedGrantAsync(quantity: 1000, expiresInDays: 60);

        await _ledger.ProcessAsync("job:a/step:1", 100);

        Assert.Equal(100, await _ledger.BalanceAsync(expiringSoon));
        Assert.Equal(0, await _ledger.BalanceAsync(laterGrant));
    }

    [Fact]
    public async Task Balance_MovesToTheNextGrant_ForWorkArrivingAfterTheFirstIsExhausted()
    {
        var expiringSoon = await _ledger.SeedGrantAsync(quantity: 100, expiresInDays: 2);
        var laterGrant = await _ledger.SeedGrantAsync(quantity: 1000, expiresInDays: 60);

        await _ledger.ProcessAsync("job:a/step:1", 100);
        await _ledger.ProcessAsync("job:b/step:1", 30);

        Assert.Equal(100, await _ledger.BalanceAsync(expiringSoon));
        Assert.Equal(30, await _ledger.BalanceAsync(laterGrant));
    }

    [Fact]
    public async Task Balance_NeverExceedsAGrantsQuantity_ForWorkThatCouldHaveFitElsewhere()
    {
        // Stronger than "the ledger totals the work done", which the old policy also satisfied while
        // charging the right amount to entirely the wrong grant.
        var small = await _ledger.SeedGrantAsync(quantity: 60, expiresInDays: 2);
        var large = await _ledger.SeedGrantAsync(quantity: 1000, expiresInDays: 60);

        await _ledger.ProcessAsync("job:a/step:1", 50);
        await _ledger.ProcessAsync("job:b/step:1", 50);
        await _ledger.ProcessAsync("job:c/step:1", 50);

        var smallBalance = await _ledger.BalanceAsync(small);
        var largeBalance = await _ledger.BalanceAsync(large);

        Assert.True(smallBalance <= 60, $"Grant of 60 was charged {smallBalance}.");
        Assert.Equal(150, smallBalance + largeBalance);
    }

    [Fact]
    public async Task Balance_SpansThreeGrants_ForWorkLargerThanTwoOfThem()
    {
        var first = await _ledger.SeedGrantAsync(quantity: 10, expiresInDays: 2);
        var second = await _ledger.SeedGrantAsync(quantity: 10, expiresInDays: 30);
        var third = await _ledger.SeedGrantAsync(quantity: 500, expiresInDays: 60);

        await _ledger.ProcessAsync("job:a/step:1", 100);

        Assert.Equal(10, await _ledger.BalanceAsync(first));
        Assert.Equal(10, await _ledger.BalanceAsync(second));
        Assert.Equal(80, await _ledger.BalanceAsync(third));
    }

    [Fact]
    public async Task ReserveAsync_Refuses_ForASecondJobAgainstAnAlreadyHeldGrant()
    {
        // The second caller has to see the first caller's hold, so it must be a row, not memory.
        await _ledger.SeedGrantAsync(quantity: 1);

        var first = await _ledger.ReserveAsync("job:a/step:1", quantity: 1);
        var second = await _ledger.ReserveAsync("job:b/step:1", quantity: 1);

        Assert.Equal(ReserveQuotaResultCode.Success, first.ResultCode);
        Assert.Equal(ReserveQuotaResultCode.InsufficientQuotaError, second.ResultCode);
    }

    [Fact]
    public async Task ReserveAsync_Succeeds_ForASecondJobAfterTheFirstReleased()
    {
        await _ledger.SeedGrantAsync(quantity: 1);

        await _ledger.ReserveAsync("job:a/step:1", quantity: 1);
        await _ledger.ReleaseAsync("job:a/step:1");
        var second = await _ledger.ReserveAsync("job:b/step:1", quantity: 1);

        Assert.Equal(ReserveQuotaResultCode.Success, second.ResultCode);
    }

    [Fact]
    public async Task ReserveAsync_Succeeds_ForASecondJobAfterTheFirstHoldExpired()
    {
        // A process killed between reserving and settling leaves a hold nothing resolves; the TTL
        // is the only thing that gives it back.
        await _ledger.SeedGrantAsync(quantity: 1);

        await _ledger.ReserveAsync("job:a/step:1", quantity: 1);
        _host.TimeProvider.Advance(
            new ReservationOptions().ReservationTtl + TimeSpan.FromMinutes(1)
        );
        var second = await _ledger.ReserveAsync("job:b/step:1", quantity: 1);

        Assert.Equal(ReserveQuotaResultCode.Success, second.ResultCode);
    }

    [Fact]
    public async Task Balance_ExceedsTheGrantQuantity_ForWorkNoGrantHadRoomFor()
    {
        var grant = await _ledger.SeedGrantAsync(quantity: 10);

        await _ledger.ProcessAsync("job:a/step:1", 500);

        Assert.Equal(500, await _ledger.BalanceAsync(grant));
    }

    [Fact]
    public async Task ReserveAsync_Refuses_ForAnAccountWhoseOnlyGrantIsOverdrawn()
    {
        // An overdrawn grant reports no room rather than negative room.
        var grant = await _ledger.SeedGrantAsync(quantity: 10);
        await _ledger.ProcessAsync("job:a/step:1", 500);

        var next = await _ledger.ReserveAsync("job:b/step:1", quantity: 1);

        Assert.Equal(ReserveQuotaResultCode.InsufficientQuotaError, next.ResultCode);
        Assert.Equal(500, await _ledger.BalanceAsync(grant));
    }

    [Fact]
    public async Task Balance_IgnoresTheExpiredGrant_ForAnAccountHoldingOneOfEach()
    {
        var expired = await _ledger.SeedGrantAsync(quantity: 100, expiresInDays: -1);
        var live = await _ledger.SeedGrantAsync(quantity: 100, expiresInDays: 30);

        await _ledger.ProcessAsync("job:a/step:1", 40);

        Assert.Equal(0, await _ledger.BalanceAsync(expired));
        Assert.Equal(40, await _ledger.BalanceAsync(live));
    }

    [Fact]
    public async Task SettleAsync_MovesTheCharge_ForAGrantThatExpiredWhileTheWorkRan()
    {
        // Settlement re-allocates against the grants live at that moment, and the hold on the
        // expired grant is given back rather than settled.
        var expiringToday = await _ledger.SeedGrantAsync(quantity: 100, expiresInDays: 1);
        var laterGrant = await _ledger.SeedGrantAsync(quantity: 100, expiresInDays: 60);

        await _ledger.ReserveAsync("job:a/step:1", quantity: 1);
        _host.TimeProvider.Advance(TimeSpan.FromDays(2));
        await _ledger.SettleAsync("job:a/step:1", 30);

        Assert.Equal(0, await _ledger.BalanceAsync(expiringToday));
        Assert.Equal(30, await _ledger.BalanceAsync(laterGrant));
    }

    [Fact]
    public async Task Balance_ChargesOnce_ForAStepReplayedUnderTheSameScope()
    {
        var grant = await _ledger.SeedGrantAsync(quantity: 100);

        await _ledger.ProcessAsync("job:a/step:1", 40);
        await _ledger.ProcessAsync("job:a/step:1", 40);

        Assert.Equal(40, await _ledger.BalanceAsync(grant));
    }

    [Fact]
    public async Task Balance_ChargesTheRetry_ForAStepRetriedAfterItsHoldWasReleased()
    {
        var grant = await _ledger.SeedGrantAsync(quantity: 100);

        await _ledger.ReserveAsync("job:a/step:1", quantity: 1);
        await _ledger.ReleaseAsync("job:a/step:1");
        var retry = await _ledger.ReserveAsync("job:a/step:1", quantity: 1);
        await _ledger.SettleAsync("job:a/step:1", 30);

        Assert.Equal(ReserveQuotaResultCode.Success, retry.ResultCode);
        Assert.Equal(30, await _ledger.BalanceAsync(grant));
    }

    [Fact]
    public async Task ReserveAsync_Succeeds_ForAReplayOfASettledStep()
    {
        var grant = await _ledger.SeedGrantAsync(quantity: 100);

        await _ledger.ProcessAsync("job:a/step:1", 40);
        var replay = await _ledger.ReserveAsync("job:a/step:1", quantity: 1);
        await _ledger.SettleAsync("job:a/step:1", 40);

        Assert.Equal(ReserveQuotaResultCode.Success, replay.ResultCode);
        Assert.Equal(40, await _ledger.BalanceAsync(grant));
    }

    [Fact]
    public async Task Balance_ChargesTwice_ForTwoDistinctJobsOverTheSameWork()
    {
        var grant = await _ledger.SeedGrantAsync(quantity: 100);

        await _ledger.ProcessAsync("job:a/step:1", 40);
        await _ledger.ProcessAsync("job:b/step:1", 40);

        Assert.Equal(80, await _ledger.BalanceAsync(grant));
    }
}
