using Corely.Billing;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Quota.Services;

namespace Corely.Billing.UnitTests.Quota.Services;

public class ExpiringFirstGrantSelectionPolicyTests
{
    private static readonly Guid TestGrantId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestGrantId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TestGrantId3 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TestAccountId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime Now = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ExpiringFirstGrantSelectionPolicy _policy = new();

    private static Grant MakeGrant(Guid id, long qty, DateTime fromUtc, DateTime toUtc)
    {
        var createResult = Grant.Create(
            accountId: TestAccountId,
            quantity: qty,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            validFromUtc: fromUtc,
            validToUtc: toUtc,
            grantId: id,
            tags: null
        );
        Assert.True(createResult.IsSuccess);
        return createResult.Value;
    }

    [Fact]
    public void Split_ReportsEverythingUnallocated_ForNoGrants()
    {
        var split = _policy.Split([], [], quantity: 5);

        Assert.Multiple(() =>
        {
            Assert.Empty(split.Shares);
            Assert.Equal(5, split.Shortfall);
        });
    }

    [Fact]
    public void Split_TakesTheWholeQuantityFromOneGrant_ForAGrantWithRoomToSpare()
    {
        var grant = MakeGrant(TestGrantId1, 100, Now.AddDays(-1), Now.AddDays(30));

        var split = _policy.Split([grant], [], quantity: 40);

        var share = Assert.Single(split.Shares);
        Assert.Multiple(() =>
        {
            Assert.Equal(TestGrantId1, share.GrantId);
            Assert.Equal(40, share.Quantity);
            Assert.Equal(0, split.Shortfall);
        });
    }

    [Fact]
    public void Split_SpendsTheSoonestExpiringFirst_ForSeveralGrantsWithRoom()
    {
        // The product decision the policy is named for: quota the customer would otherwise lose is
        // spent before quota that keeps.
        var soon = MakeGrant(TestGrantId1, 100, Now.AddDays(-1), Now.AddDays(2));
        var later = MakeGrant(TestGrantId2, 100, Now.AddDays(-1), Now.AddDays(30));

        var split = _policy.Split([later, soon], [], quantity: 10);

        Assert.Equal(TestGrantId1, Assert.Single(split.Shares).GrantId);
    }

    [Fact]
    public void Split_SpreadsAcrossGrants_ForWorkLargerThanTheFirstGrantsRemainder()
    {
        // The bug this replaces. Selecting on "has any room left" charged a five-hundred-page
        // document entirely to a grant with one page free, leaving it 499 overspent and the next
        // grant untouched. Nothing blocked, so it was invisible until someone reconciled an invoice.
        var almostSpent = MakeGrant(TestGrantId1, 100, Now.AddDays(-1), Now.AddDays(2));
        var fresh = MakeGrant(TestGrantId2, 1000, Now.AddDays(-1), Now.AddDays(30));

        var split = _policy.Split(
            [almostSpent, fresh],
            [new GrantTotalConsumptions(TestGrantId1, 99)],
            quantity: 500
        );

        Assert.Multiple(() =>
        {
            Assert.Equal(2, split.Shares.Count);
            Assert.Equal(new(TestGrantId1, 1), split.Shares[0]);
            Assert.Equal(new(TestGrantId2, 499), split.Shares[1]);
            Assert.Equal(0, split.Shortfall);
        });
    }

    [Fact]
    public void Split_UsesOneGrant_ForWorkThatExactlyFillsIt()
    {
        // The boundary itself. One page either side of this is a different shape, so it is worth
        // pinning rather than inferring from the two neighbours below.
        var almostSpent = MakeGrant(TestGrantId1, 100, Now.AddDays(-1), Now.AddDays(2));
        var fresh = MakeGrant(TestGrantId2, 1000, Now.AddDays(-1), Now.AddDays(30));

        var split = _policy.Split(
            [almostSpent, fresh],
            [new GrantTotalConsumptions(TestGrantId1, 60)],
            quantity: 40
        );

        var share = Assert.Single(split.Shares);
        Assert.Multiple(() =>
        {
            Assert.Equal(TestGrantId1, share.GrantId);
            Assert.Equal(40, share.Quantity);
            Assert.Equal(0, split.Shortfall);
        });
    }

    [Fact]
    public void Split_TakesOnePageFromTheNextGrant_ForWorkOnePageOverTheEdge()
    {
        // Off by one in the direction that matters: the old code charged all 41 to the first grant.
        var almostSpent = MakeGrant(TestGrantId1, 100, Now.AddDays(-1), Now.AddDays(2));
        var fresh = MakeGrant(TestGrantId2, 1000, Now.AddDays(-1), Now.AddDays(30));

        var split = _policy.Split(
            [almostSpent, fresh],
            [new GrantTotalConsumptions(TestGrantId1, 60)],
            quantity: 41
        );

        Assert.Equal(
            [new GrantShare(TestGrantId1, 40), new GrantShare(TestGrantId2, 1)],
            split.Shares
        );
    }

    [Fact]
    public void Split_SpendsTheLastPage_ForAGrantWithExactlyOneLeft()
    {
        // Literally the case from the bug report: a grant with one page remaining was selected for a
        // five-hundred-page document and charged all five hundred.
        var onePageLeft = MakeGrant(TestGrantId1, 100, Now.AddDays(-1), Now.AddDays(2));
        var fresh = MakeGrant(TestGrantId2, 1000, Now.AddDays(-1), Now.AddDays(30));

        var split = _policy.Split(
            [onePageLeft, fresh],
            [new GrantTotalConsumptions(TestGrantId1, 99)],
            quantity: 500
        );

        Assert.Equal(
            [new GrantShare(TestGrantId1, 1), new GrantShare(TestGrantId2, 499)],
            split.Shares
        );
    }

    [Fact]
    public void Split_SkipsGrantsWithNothingLeft_ForAFullyConsumedGrant()
    {
        var spent = MakeGrant(TestGrantId1, 10, Now.AddDays(-10), Now.AddDays(1));
        var available = MakeGrant(TestGrantId2, 5, Now.AddDays(-5), Now.AddDays(2));

        var split = _policy.Split(
            [spent, available],
            [new GrantTotalConsumptions(TestGrantId1, 10)],
            quantity: 3
        );

        Assert.Equal(TestGrantId2, Assert.Single(split.Shares).GrantId);
    }

    [Fact]
    public void Split_AddsNoCapacityBack_ForAnAlreadyOverdrawnGrant()
    {
        // Overdraft-once leaves grants reading past their quantity. Subtracting naively would make
        // an overdrawn grant look like it had negative room, and negative room must not become
        // capacity somewhere else.
        var overdrawn = MakeGrant(TestGrantId1, 10, Now.AddDays(-10), Now.AddDays(1));
        var available = MakeGrant(TestGrantId2, 50, Now.AddDays(-5), Now.AddDays(2));

        var split = _policy.Split(
            [overdrawn, available],
            [new GrantTotalConsumptions(TestGrantId1, 500)],
            quantity: 20
        );

        var share = Assert.Single(split.Shares);
        Assert.Multiple(() =>
        {
            Assert.Equal(TestGrantId2, share.GrantId);
            Assert.Equal(20, share.Quantity);
        });
    }

    [Fact]
    public void Split_ReportsTheShortfall_ForWorkLargerThanEveryGrantTogether()
    {
        // Reported rather than refused. Before the work this is insufficient quota; afterwards the
        // provider has already been paid and the same number becomes the overdraft.
        var first = MakeGrant(TestGrantId1, 10, Now.AddDays(-1), Now.AddDays(2));
        var second = MakeGrant(TestGrantId2, 5, Now.AddDays(-1), Now.AddDays(30));

        var split = _policy.Split([first, second], [], quantity: 100);

        Assert.Multiple(() =>
        {
            Assert.Equal(2, split.Shares.Count);
            Assert.Equal(15, split.Shares.Sum(a => a.Quantity));
            Assert.Equal(85, split.Shortfall);
        });
    }

    [Fact]
    public void Split_AllocatesNothing_ForAQuantityOfZero()
    {
        var grant = MakeGrant(TestGrantId1, 100, Now.AddDays(-1), Now.AddDays(2));

        var split = _policy.Split([grant], [], quantity: 0);

        Assert.Multiple(() =>
        {
            Assert.Empty(split.Shares);
            Assert.Equal(0, split.Shortfall);
        });
    }

    [Fact]
    public void Split_BreaksExpiryTiesBySize_ForGrantsExpiringTogether()
    {
        // The order has to be total, or a replay of the same work allocates differently and the
        // idempotency keys no longer line up.
        var expiry = Now.AddDays(2);
        var larger = MakeGrant(TestGrantId1, 100, Now.AddDays(-1), expiry);
        var smaller = MakeGrant(TestGrantId2, 10, Now.AddDays(-1), expiry);

        var split = _policy.Split([larger, smaller], [], quantity: 5);

        Assert.Equal(TestGrantId2, Assert.Single(split.Shares).GrantId);
    }

    [Fact]
    public void Split_Throws_ForANegativeQuantity() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _policy.Split([], [], quantity: -1));

    [Fact]
    public void Split_IgnoresTotalsForOtherGrants_ForAnUnrelatedGrantId()
    {
        var grant = MakeGrant(TestGrantId1, 10, Now.AddDays(-1), Now.AddDays(2));

        var split = _policy.Split(
            [grant],
            [new GrantTotalConsumptions(TestGrantId3, 999)],
            quantity: 10
        );

        Assert.Equal(10, Assert.Single(split.Shares).Quantity);
    }
}
