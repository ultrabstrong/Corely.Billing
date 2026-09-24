using Corely.Billing.Consumption.Models;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class TimeBucketExtensionsTests
{
    [Theory]
    [InlineData(TimeBucket.Day, "Mar 2")]
    [InlineData(TimeBucket.Week, "Week of Mar 2")]
    [InlineData(TimeBucket.Month, "Mar 2026")]
    public void PeriodLabel_NamesThePeriod_ForEachBucket(TimeBucket bucket, string expected) =>
        Assert.Equal(
            expected,
            bucket.PeriodLabel(new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc))
        );
}
