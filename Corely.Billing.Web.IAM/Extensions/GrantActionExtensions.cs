using Corely.Billing.IAM;
using Corely.IAM.Security.Enums;

namespace Corely.Billing.Web.IAM.Extensions;

internal static class GrantActionExtensions
{
    extension(GrantAction action)
    {
        public IReadOnlyList<(AuthAction Action, string Resource)> Permissions() =>
            action switch
            {
                GrantAction.Create =>
                [
                    (AuthAction.Create, BillingResourceTypes.GRANT_RESOURCE_TYPE),
                ],
                GrantAction.Update =>
                [
                    (AuthAction.Update, BillingResourceTypes.GRANT_RESOURCE_TYPE),
                ],
                GrantAction.Delete =>
                [
                    (AuthAction.Delete, BillingResourceTypes.GRANT_RESOURCE_TYPE),
                ],
                GrantAction.Request =>
                [
                    (AuthAction.Create, BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE),
                ],
                GrantAction.Decide =>
                [
                    (AuthAction.Update, BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE),
                    (AuthAction.Create, BillingResourceTypes.GRANT_RESOURCE_TYPE),
                ],
                GrantAction.ManageRegistrations =>
                [
                    (AuthAction.Execute, BillingResourceTypes.QUOTA_RESOURCE_TYPE),
                ],
            };
    }
}
