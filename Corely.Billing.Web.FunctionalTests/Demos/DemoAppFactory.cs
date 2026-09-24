using Corely.Billing.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Web.FunctionalTests.Demos;

public sealed class DemoAppFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public DemoAppFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAllKeyed<IEFConfiguration>(EFConfigurationKeys.BILLING);
            services.AddKeyedScoped<IEFConfiguration>(
                EFConfigurationKeys.BILLING,
                (_, _) => new SqliteEFConfiguration(_connection)
            );
            services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        });
    }

    public void CreateBillingSchema()
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.EnsureCreated();
    }

    public HttpClient CreateTestClient() =>
        CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
            }
        );

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }

    private sealed class SqliteEFConfiguration(SqliteConnection connection)
        : EFSqliteConfigurationBase(connection.ConnectionString)
    {
        public override void Configure(DbContextOptionsBuilder optionsBuilder) =>
            optionsBuilder.UseSqlite(connection);
    }
}
