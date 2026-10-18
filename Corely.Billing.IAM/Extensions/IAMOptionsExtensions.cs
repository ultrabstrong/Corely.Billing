using Corely.IAM;
using Corely.IAM.Security.Enums;

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
                    BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE,
                    BillingResourceTypes.GRANT_REQUEST_DESCRIPTION,
                    AuthAction.Read,
                    AuthAction.Create
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
