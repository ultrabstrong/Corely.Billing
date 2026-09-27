using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Operations;

namespace Corely.Billing.UnitTests.Consumption.Models;

public class ConsumptionEventTests
{
    private static readonly Guid GrantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData("job:a/step:b")]
    [InlineData("chat:7|turn:2")]
    public void IdempotencyScope_ReturnsTheScope_ForAKeyTheFactoryBuilt(string scope)
    {
        var row = Row(
            IdempotencyKeyFactory.Create(
                new OperationContext(Guid.CreateVersion7(), scope),
                TestUsage.Extraction,
                TestUsage.Page,
                GrantId
            )
        );

        Assert.Equal(scope, row.IdempotencyScope);
    }

    [Fact]
    public void IdempotencyScope_ReturnsNull_ForAKeyOfAnotherShape()
    {
        Assert.Null(Row("not-a-ledger-key").IdempotencyScope);
    }

    [Theory]
    [InlineData(ConsumptionOutcome.Settled, 0, true)]
    [InlineData(ConsumptionOutcome.Released, 0, false)]
    [InlineData(null, 1, true)]
    [InlineData(null, -1, false)]
    public void CountsTowardBalance_FollowsTheLedgerRule_ForEachState(
        ConsumptionOutcome? outcome,
        int hoursAfterLiveFrom,
        bool expected
    )
    {
        var liveFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var row = Row("k");
        row.Outcome = outcome;
        row.FinalizedUtc = outcome is null ? null : liveFrom;
        row.UtcTimestamp = liveFrom.AddHours(hoursAfterLiveFrom);

        Assert.Equal(expected, row.CountsTowardBalance(liveFrom));
    }

    private static ConsumptionEvent Row(string key) =>
        new()
        {
            GrantId = GrantId,
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            IdempotencyKey = key,
            Provider = "p",
        };
}
