using Corely.Billing.Grants.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.UnitTests.Grants;

internal static class ServiceFixture
{
    public static T GetRequiredService<T>()
        where T : notnull =>
        SqliteServiceFactory
            .RegisterEFReposAndUoWServices<EntitlementsDbContext>()
            .GetRequiredService<T>();
}
