using Corely.IAM.Users.Models;

namespace Corely.Billing.IAM.Extensions;

internal static class UserContextExtensions
{
    extension(UserContext? context)
    {
        public Guid? ActingUserId(Guid? claimed) =>
            context is { IsSystemContext: false } ? context.User?.Id : claimed;
    }
}
