using Corely.Billing.Consumption.Extensions;
using Corely.Billing.Consumption.Models;

namespace Corely.Billing.UnitTests.Consumption.Extensions;

public class TimeBucketExtensionsTests
{
    [Theory]
    [InlineData(TimeBucket.Day, "2026-03-04T17:45:00", "2026-03-04T00:00:00")]
    [InlineData(TimeBucket.Week, "2026-03-04T17:45:00", "2026-03-02T00:00:00")]
    [InlineData(TimeBucket.Week, "2026-03-02T00:00:00", "2026-03-02T00:00:00")]
    [InlineData(TimeBucket.Week, "2026-03-08T23:59:59", "2026-03-02T00:00:00")]
    [InlineData(TimeBucket.Week, "2026-01-01T12:00:00", "2025-12-29T00:00:00")]
    [InlineData(TimeBucket.Month, "2026-03-31T23:59:59", "2026-03-01T00:00:00")]
    public void BucketStart_IsTheStartOfTheContainingBucket_ForAnInstant(
        TimeBucket bucket,
        string instant,
        string expected
    )
    {
        var start = bucket.BucketStart(Utc(instant));

        Assert.Equal(Utc(expected), start);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    [Theory]
    [InlineData(TimeBucket.Day, "2026-02-28T00:00:00", "2026-03-01T00:00:00")]
    [InlineData(TimeBucket.Week, "2026-12-28T00:00:00", "2027-01-04T00:00:00")]
    [InlineData(TimeBucket.Month, "2026-01-01T00:00:00", "2026-02-01T00:00:00")]
    [InlineData(TimeBucket.Month, "2026-12-01T00:00:00", "2027-01-01T00:00:00")]
    public void NextBucketStart_IsTheFollowingBucket_ForABucketStart(
        TimeBucket bucket,
        string start,
        string expected
    ) => Assert.Equal(Utc(expected), bucket.NextBucketStart(Utc(start)));

    private static DateTime Utc(string value) =>
        DateTime.SpecifyKind(DateTime.Parse(value), DateTimeKind.Utc);
}
