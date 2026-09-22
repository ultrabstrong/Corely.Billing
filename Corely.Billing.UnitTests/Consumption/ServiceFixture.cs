using Corely.Billing.Consumption.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.UnitTests.Consumption;

internal static class ServiceFixture
{
    public static T GetRequiredService<T>()
        where T : notnull =>
        SqliteServiceFactory
            .RegisterEFReposAndUoWServices<MeteringDbContext>()
            .GetRequiredService<T>();
}
