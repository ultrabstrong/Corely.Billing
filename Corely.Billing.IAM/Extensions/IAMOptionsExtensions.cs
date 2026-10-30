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
                    BillingResourceTypes.GRANT_TITLE,
                    BillingResourceTypes.GRANT_DESCRIPTION,
                    BillingResourceTypes.GROUP,
                    AuthAction.Read
                )
                .RegisterResourceType(
                    BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE,
                    BillingResourceTypes.GRANT_REQUEST_TITLE,
                    BillingResourceTypes.GRANT_REQUEST_DESCRIPTION,
                    BillingResourceTypes.GROUP,
                    AuthAction.Read,
                    AuthAction.Create
                )
                .RegisterResourceType(
                    BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE,
                    BillingResourceTypes.CONSUMPTION_TITLE,
                    BillingResourceTypes.CONSUMPTION_DESCRIPTION,
                    BillingResourceTypes.GROUP,
                    AuthAction.Read
                )
                .RegisterResourceType(
                    BillingResourceTypes.QUOTA_RESOURCE_TYPE,
                    BillingResourceTypes.QUOTA_TITLE,
                    BillingResourceTypes.QUOTA_DESCRIPTION,
                    BillingResourceTypes.GROUP,
                    AuthAction.Read
                )
                .RegisterResourceType(
                    BillingResourceTypes.REGISTRATION_RESOURCE_TYPE,
                    BillingResourceTypes.REGISTRATION_TITLE,
                    BillingResourceTypes.REGISTRATION_DESCRIPTION,
                    BillingResourceTypes.GROUP,
                    AuthAction.Read,
                    AuthAction.Execute
                )
                .RegisterResourceType(
                    BillingResourceTypes.ALL_ACCOUNTS_RESOURCE_TYPE,
                    BillingResourceTypes.ALL_ACCOUNTS_TITLE,
                    BillingResourceTypes.ALL_ACCOUNTS_DESCRIPTION,
                    BillingResourceTypes.GROUP
                );
    }
}
