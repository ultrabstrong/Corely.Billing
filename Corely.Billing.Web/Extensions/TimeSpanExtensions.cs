namespace Corely.Billing.Web.Extensions;

internal static class TimeSpanExtensions
{
    extension(TimeSpan span)
    {
        public string AheadText() =>
            (int)Math.Floor(span.TotalDays) switch
            {
                <= 0 => "today",
                1 => "tomorrow",
                var days => $"in {days:N0} days",
            };

        public string AgoText() =>
            (int)Math.Floor(span.TotalDays) switch
            {
                <= 0 => "today",
                1 => "yesterday",
                var days => $"{days:N0} days ago",
            };
    }
}
