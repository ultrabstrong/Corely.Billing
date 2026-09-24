using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.UnitTests.Quota.Models;

public class QuotaContextTests
{
    private static readonly Guid GrantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Theory]
    [InlineData(0, 0, 1.0)]
    [InlineData(20, 0, 0.8)]
    [InlineData(20, 30, 0.5)]
    [InlineData(100, 0, 0.0)]
    [InlineData(500, 0, 0.0)]
    public void RemainingRatio_IsWhatIsLeftAfterTheCharge_ForOneLimitedGrant(
        long consumed,
        long charged,
        double expected
    )
    {
        var context = Context([Grant(GrantId1, 100)], [new(GrantId1, consumed)]);

        Assert.Equal(expected, context.RemainingRatio(charged), precision: 6);
    }

    [Fact]
    public void RemainingRatio_PoolsEveryGrant_ForSeveralLimitedGrants()
    {
        var context = Context(
            [Grant(GrantId1, 100), Grant(GrantId2, 300)],
            [new(GrantId1, 100), new(GrantId2, 100)]
        );

        Assert.Equal(0.5, context.RemainingRatio(charged: 0), precision: 6);
    }

    [Fact]
    public void RemainingRatio_IsFull_ForAnUnlimitedGrantBesideAnExhaustedOne()
    {
        var context = Context(
            [Grant(GrantId1, 10), Grant(GrantId2, null)],
            [new(GrantId1, 10), new(GrantId2, long.MaxValue)]
        );

        Assert.Equal(1, context.RemainingRatio(charged: long.MaxValue));
    }

    [Fact]
    public void RemainingRatio_IsEmpty_ForNoGrants() =>
        Assert.Equal(0, Context([], []).RemainingRatio(charged: 5));

    [Fact]
    public void RemainingRatio_IsEmpty_ForGrantsOfZero() =>
        Assert.Equal(0, Context([Grant(GrantId1, 0)], []).RemainingRatio(charged: 0));

    [Fact]
    public void RemainingRatio_CountsAGrantWithNoTotal_AsUnused() =>
        Assert.Equal(0.9, Context([Grant(GrantId1, 100)], []).RemainingRatio(10), precision: 6);

    [Fact]
    public void WithoutHolds_SubtractsEachHoldFromItsOwnGrant_ForHoldsOnTwoGrants()
    {
        var context = Context(
            [Grant(GrantId1, 100), Grant(GrantId2, 100)],
            [new(GrantId1, 40), new(GrantId2, 10)]
        );

        var released = context.WithoutHolds([
            Hold(GrantId1, 15),
            Hold(GrantId1, 5),
            Hold(GrantId2, 10),
        ]);

        Assert.Equal(
            [new GrantTotalConsumptions(GrantId1, 20), new GrantTotalConsumptions(GrantId2, 0)],
            released.Totals
        );
        Assert.Same(context.Grants, released.Grants);
    }

    [Fact]
    public void WithoutHolds_LeavesTheContextUnchanged_ForNoHolds()
    {
        var context = Context([Grant(GrantId1, 100)], [new(GrantId1, 40)]);

        Assert.Equal(context.Totals, context.WithoutHolds([]).Totals);
    }

    private static QuotaContext Context(List<Grant> grants, List<GrantTotalConsumptions> totals) =>
        new(grants, totals);

    private static Grant Grant(Guid grantId, long? quantity) =>
        new()
        {
            GrantId = grantId,
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = quantity,
        };

    private static ConsumptionEvent Hold(Guid grantId, long quantity) =>
        new()
        {
            GrantId = grantId,
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = quantity,
            Provider = "prov",
            IdempotencyKey = "key",
        };
}
