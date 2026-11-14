namespace Corely.Billing.Web;

public static class UsageText
{
    public const string UNLIMITED = "Unlimited";

    public static string Number(decimal value) => value.ToString("#,0.######");

    public static string Count(decimal count, string unitDisplayName) =>
        $"{Number(count)} {unitDisplayName}{(count == 1 ? string.Empty : "s")}";

    public static string Count(decimal? count, string unitDisplayName) =>
        count is { } value ? Count(value, unitDisplayName) : $"{UNLIMITED} {unitDisplayName}s";
}
