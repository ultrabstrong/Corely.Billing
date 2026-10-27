using Corely.IAM.Users.Providers;

namespace Corely.Billing.Web.IAM.Extensions;

internal static class UserContextProviderExtensions
{
    extension(IUserContextProvider provider)
    {
        public Guid? CurrentAccountId => provider.GetUserContext()?.CurrentAccount?.Id;
    }
}
