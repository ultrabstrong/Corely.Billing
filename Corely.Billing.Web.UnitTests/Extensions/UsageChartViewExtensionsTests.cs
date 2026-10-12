using Corely.Billing.Web.Components;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class UsageChartViewExtensionsTests
{
    [Theory]
    [InlineData(UsageChartView.Used, false)]
    [InlineData(UsageChartView.Stacked, false)]
    [InlineData(UsageChartView.SideBySide, false)]
    [InlineData(UsageChartView.Remaining, true)]
    [InlineData(UsageChartView.BurnUp, true)]
    [InlineData(UsageChartView.Share, false)]
    public void ReadsGrants_IsTrueOnlyForBalanceViews_ForEachView(
        UsageChartView view,
        bool expected
    ) => Assert.Equal(expected, view.ReadsGrants());
}
