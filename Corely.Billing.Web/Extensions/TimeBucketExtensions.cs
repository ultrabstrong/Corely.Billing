using Corely.Billing.Consumption.Models;

namespace Corely.Billing.Web.Extensions;

internal static class TimeBucketExtensions
{
    extension(TimeBucket bucket)
    {
        public string PeriodLabel(DateTime bucketStart) =>
            bucket switch
            {
                TimeBucket.Week => $"Week of {bucketStart:MMM d}",
                TimeBucket.Month => bucketStart.ToString("MMM yyyy"),
                _ => bucketStart.ToString("MMM d"),
            };
    }
}
