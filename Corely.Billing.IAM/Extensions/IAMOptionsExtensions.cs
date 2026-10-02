using Corely.IAM;
using Corely.IAM.Security.Constants;

namespace Corely.Billing.IAM.Extensions;

public static class IAMOptionsExtensions
{
    extension(IAMOptions options)
    {
        public IAMOptions RegisterBillingResourceTypes() =>
            options
                .RegisterResourceType(
                    BillingResourceTypes.GRANT_RESOURCE_TYPE,
                    BillingResourceTypes.GRANT_DESCRIPTION,
                    AuthAction.Read
                )
                .RegisterResourceType(
                    BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE,
                    BillingResourceTypes.CONSUMPTION_DESCRIPTION,
                    AuthAction.Read
                )
                .RegisterResourceType(
                    BillingResourceTypes.QUOTA_RESOURCE_TYPE,
                    BillingResourceTypes.QUOTA_DESCRIPTION,
                    AuthAction.Read,
                    AuthAction.Execute
                );
    }
}
