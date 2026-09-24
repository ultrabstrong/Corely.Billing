namespace Corely.Billing.Web;

public static class UsageText
{
    public const string UNLIMITED = "Unlimited";

    public static string Count(long count, string unitDisplayName) =>
        $"{count:N0} {unitDisplayName}{(count == 1 ? string.Empty : "s")}";

    public static string Count(long? count, string unitDisplayName) =>
        count is { } value ? Count(value, unitDisplayName) : $"{UNLIMITED} {unitDisplayName}s";
}
