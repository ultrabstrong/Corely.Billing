using Corely.Billing.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class UsageDashboardTests : BillingWebTestContext
{
    [Fact]
    public void Render_LoadsTheChartAndTable_ForAnAccountWithNoUsage()
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

        var dashboard = Render<UsageDashboard>(p => p.Add(c => c.AccountId, AccountId));

        dashboard.WaitForAssertion(
            () => Assert.Contains("Nothing was used in this range.", dashboard.Markup),
            TimeSpan.FromSeconds(5)
        );
    }
}
