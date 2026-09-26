using Corely.IAM;

namespace Corely.Billing.IAM.Extensions;

public static class IAMOptionsExtensions
{
    extension(IAMOptions options)
    {
        public IAMOptions RegisterBillingResourceTypes() =>
            options
                .RegisterResourceType(
                    BillingResourceTypes.GRANT_RESOURCE_TYPE,
                    BillingResourceTypes.GRANT_DESCRIPTION
                )
                .RegisterResourceType(
                    BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE,
                    BillingResourceTypes.CONSUMPTION_DESCRIPTION
                )
                .RegisterResourceType(
                    BillingResourceTypes.QUOTA_RESOURCE_TYPE,
                    BillingResourceTypes.QUOTA_DESCRIPTION
                );
    }
}
