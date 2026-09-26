using Corely.IAM.Security.Constants;

namespace Corely.Billing.Web.IAM.Extensions;

internal static class GrantActionExtensions
{
    extension(GrantAction action)
    {
        public AuthAction ToAuthAction() =>
            action switch
            {
                GrantAction.Create => AuthAction.Create,
                GrantAction.Update => AuthAction.Update,
                GrantAction.Delete => AuthAction.Delete,
            };
    }
}
