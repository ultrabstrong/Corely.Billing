using Corely.Billing.Models;

namespace Corely.Billing.Web.Components;

internal static class BillingMessages
{
    public const string NO_ACCOUNT = "No account is selected.";

    public static string RetrieveError(RetrieveResultCode code, string action, string message) =>
        code == RetrieveResultCode.UnauthorizedError ? $"You are not allowed to {action}."
        : string.IsNullOrWhiteSpace(message) ? $"Could not {action}."
        : message;

    public static string InDays(TimeSpan span) =>
        (int)Math.Floor(span.TotalDays) switch
        {
            0 => "today",
            1 => "tomorrow",
            var days => $"in {days:N0} days",
        };

    public static string DaysAgo(TimeSpan span) =>
        (int)Math.Floor(span.TotalDays) switch
        {
            0 => "today",
            1 => "yesterday",
            var days => $"{days:N0} days ago",
        };
}
