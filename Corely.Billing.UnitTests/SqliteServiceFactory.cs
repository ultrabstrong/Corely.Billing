using Corely.DataAccess.EntityFramework.Configurations;
using Corely.DataAccess.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Corely.Billing.UnitTests;

internal static class SqliteServiceFactory
{
    private sealed class SqliteConfiguration : EFSqliteConfigurationBase
    {
        private readonly SqliteConnection _connection;

        public SqliteConfiguration(string connectionString)
            : base(connectionString)
        {
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
        }

        public override void Configure(DbContextOptionsBuilder b) => b.UseSqlite(_connection);
    }

    public static IServiceProvider RegisterEFReposAndUoWServices<TContext>()
        where TContext : DbContext
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        services.AddDbContext<TContext>();
        services.AddSingleton<IEFConfiguration>(
            new SqliteConfiguration(
                $"Data Source={Guid.CreateVersion7():N};Mode=Memory;Cache=Shared"
            )
        );
        services.RegisterEntityFrameworkReposAndUoW();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureCreated();
        }

        return provider;
    }
}
