namespace Corely.Billing.IAM;

public static class BillingResourceTypes
{
    public const string GRANT_RESOURCE_TYPE = "grant";
    public const string GRANT_REQUEST_RESOURCE_TYPE = "grant_request";
    public const string CONSUMPTION_RESOURCE_TYPE = "consumption";
    public const string QUOTA_RESOURCE_TYPE = "quota";
    public const string ALL_ACCOUNTS_RESOURCE_TYPE = "billing_all_accounts";

    public const string GRANT_DESCRIPTION = "Billing grants: what an account may use";
    public const string GRANT_REQUEST_DESCRIPTION =
        "Billing grant requests: asking for a grant, and approving or declining one";
    public const string CONSUMPTION_DESCRIPTION = "Billing consumption: what an account used";
    public const string QUOTA_DESCRIPTION =
        "Billing quota: reading balances, allowances, features and capacity, reserving and "
        + "settling usage, and registering against capacity";
    public const string ALL_ACCOUNTS_DESCRIPTION =
        "Billing reads across every account, for a role in the platform account";
}
