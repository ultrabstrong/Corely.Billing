using Corely.Billing.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class UsageDashboardTests : BillingWebTestContext
{
    public UsageDashboardTests()
    {
        Consumption
            .Setup(c => c.ListProvidersAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RetrieveSingleResult<List<string>>(RetrieveResultCode.Success, "", [])
            );
        Consumption
            .Setup(c =>
                c.GetEarliestConsumptionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new RetrieveSingleResult<DateTime?>(RetrieveResultCode.Success, "", null)
            );
    }

    [Fact]
    public void Render_LoadsTheChartAndTable_ForAnAccountWithNoUsage()
    {
        var dashboard = Render();

        dashboard.WaitForAssertion(
            () => Assert.Contains("Nothing was used in this range.", dashboard.Markup),
            TimeSpan.FromSeconds(5)
        );
    }

    [Fact]
    public void Render_OffersGrants_ForACallerWhoCanReadThem()
    {
        var dashboard = Render();

        dashboard.WaitForAssertion(() =>
        {
            Assert.NotNull(dashboard.Find(".cbw-filters [aria-label='Grant']"));
            Assert.True(dashboard.FindComponent<UsageChart>().Instance.ShowGrants);
            Assert.True(dashboard.FindComponent<ConsumptionTable>().Instance.ShowGrants);
        });
    }

    [Fact]
    public void Render_ShowsUsageWithoutGrants_ForACallerWhoCannotReadThem()
    {
        DenyGrants();

        var dashboard = Render();

        dashboard.WaitForAssertion(() =>
        {
            Assert.DoesNotContain("alert-danger", dashboard.Markup);
            Assert.Empty(dashboard.FindAll(".cbw-filters [aria-label='Grant']"));
            Assert.False(dashboard.FindComponent<UsageChart>().Instance.ShowGrants);
            Assert.False(dashboard.FindComponent<ConsumptionTable>().Instance.ShowGrants);
        });
    }

    private IRenderedComponent<UsageDashboard> Render() =>
        Render<UsageDashboard>(p => p.Add(c => c.AccountId, AccountId));
}
