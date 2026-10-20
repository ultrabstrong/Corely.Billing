namespace Corely.Billing.Demos.WithIAM;

public static class DemoRoutes
{
    public static string Account(Guid accountId) => $"/accounts/{accountId}";

    public static string Grants(Guid accountId) => $"{Account(accountId)}/grants";

    public static string GrantNew(Guid accountId) => $"{Grants(accountId)}/new";

    public static string GrantRequest(Guid accountId) => $"{Grants(accountId)}/request";

    public static string Grant(Guid accountId, Guid grantId) => $"{Grants(accountId)}/{grantId}";

    public static string Usage(Guid accountId) => $"{Account(accountId)}/usage";
}
