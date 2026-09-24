namespace Corely.Billing.Web;

public static class BillingWebRoutes
{
    public const string GRANTS = "/grants";
    public const string GRANT_NEW = "/grants/new";
    public const string USAGE = "/usage";

    public static string GrantEditor(Guid grantId) => $"/grants/{grantId}";
}
