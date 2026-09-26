namespace Corely.Billing.IAM;

public static class BillingResourceTypes
{
    public const string GRANT_RESOURCE_TYPE = "grant";
    public const string CONSUMPTION_RESOURCE_TYPE = "consumption";
    public const string QUOTA_RESOURCE_TYPE = "quota";

    public const string GRANT_DESCRIPTION = "Billing grants: what an account may use";
    public const string CONSUMPTION_DESCRIPTION = "Billing consumption: what an account used";
    public const string QUOTA_DESCRIPTION =
        "Billing quota: checking availability, and reserving, settling and releasing it";
}
