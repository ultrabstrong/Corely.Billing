using Corely.Billing.Consumption.Models;

namespace Corely.Billing.Consumption.Extensions;

public static class TimeBucketExtensions
{
    extension(TimeBucket bucket)
    {
        public DateTime BucketStart(DateTime utc) =>
            bucket switch
            {
                TimeBucket.Week => Midnight(utc)
                    .AddDays(-((7 + (utc.DayOfWeek - DayOfWeek.Monday)) % 7)),
                TimeBucket.Month => new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => Midnight(utc),
            };

        public DateTime NextBucketStart(DateTime bucketStart) =>
            bucket switch
            {
                TimeBucket.Week => bucketStart.AddDays(7),
                TimeBucket.Month => bucketStart.AddMonths(1),
                _ => bucketStart.AddDays(1),
            };
    }

    private static DateTime Midnight(DateTime utc) =>
        new(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
}
