namespace Corely.Billing.Demos.Portal;

public static class PortalRoutes
{
    public const string GRANTS = "/grants";
    public const string GRANT_NEW = "/grants/new";
    public const string GRANT_REQUEST = "/grants/request";
    public const string USAGE = "/usage";

    public static string Grant(Guid grantId) => $"/grants/{grantId}";
}
