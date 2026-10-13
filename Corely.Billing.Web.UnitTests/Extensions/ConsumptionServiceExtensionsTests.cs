using Corely.Billing.Consumption.Models;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class ConsumptionServiceExtensionsTests : BillingWebTestContext
{
    [Fact]
    public async Task UsedByGrantAsync_ReturnsEachGrantsUse_ForAnAllowedRead()
    {
        var grant = Grant(100);
        HaveUsed(new GrantTotalConsumptions(grant.GrantId, 42));

        var used = await Consumption.Object.UsedByGrantAsync(AccountId, [grant]);

        Assert.Equal(new Dictionary<Guid, long> { [grant.GrantId] = 42 }, used);
    }

    [Fact]
    public async Task UsedByGrantAsync_ReturnsNull_ForARefusedRead()
    {
        DenyUsed();

        var used = await Consumption.Object.UsedByGrantAsync(AccountId, [Grant(100)]);

        Assert.Null(used);
    }

    [Fact]
    public async Task UsedByGrantAsync_ReadsNothing_ForNoGrants()
    {
        DenyUsed();

        var used = await Consumption.Object.UsedByGrantAsync(AccountId, []);

        Assert.Equal(new Dictionary<Guid, long>(), used);
        Consumption.Verify(
            c =>
                c.GetGrantConsumptionTotalsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}
