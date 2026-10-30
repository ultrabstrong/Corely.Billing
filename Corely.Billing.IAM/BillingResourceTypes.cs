namespace Corely.Billing.IAM;

public static class BillingResourceTypes
{
    public const string GRANT_RESOURCE_TYPE = "grant";
    public const string GRANT_REQUEST_RESOURCE_TYPE = "grant_request";
    public const string CONSUMPTION_RESOURCE_TYPE = "consumption";
    public const string QUOTA_RESOURCE_TYPE = "quota";
    public const string REGISTRATION_RESOURCE_TYPE = "registration";
    public const string ALL_ACCOUNTS_RESOURCE_TYPE = "billing_all_accounts";

    public const string GROUP = "Billing";

    public const string GRANT_TITLE = "Grants";
    public const string GRANT_DESCRIPTION = "What an account may use";
    public const string GRANT_REQUEST_TITLE = "Grant requests";
    public const string GRANT_REQUEST_DESCRIPTION =
        "Asking for a grant, and approving or declining one";
    public const string CONSUMPTION_TITLE = "Consumption";
    public const string CONSUMPTION_DESCRIPTION = "What an account used";
    public const string QUOTA_TITLE = "Quota";
    public const string QUOTA_DESCRIPTION =
        "Reading balances, allowances, features and capacity, and reserving and settling usage";
    public const string REGISTRATION_TITLE = "Registrations";
    public const string REGISTRATION_DESCRIPTION = "Taking, releasing and moving units of capacity";
    public const string ALL_ACCOUNTS_TITLE = "All accounts";
    public const string ALL_ACCOUNTS_DESCRIPTION =
        "Billing reads across every account, for a role in the platform account";
}
