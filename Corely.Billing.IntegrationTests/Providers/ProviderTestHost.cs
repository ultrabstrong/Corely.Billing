using Corely.Billing.DataAccess;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Operations;
using Corely.DataAccess.EntityFramework.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.MsSql;
using Testcontainers.MySql;

namespace Corely.Billing.IntegrationTests.Providers;

public sealed class ProviderTestHost(DatabaseProvider provider) : IBillingTestHost, IAsyncLifetime
{
    private IContainer? _container;
    private ServiceProvider? _serviceProvider;

    public FakeTimeProvider TimeProvider { get; } =
        new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    public DatabaseProvider Provider => provider;

    public async ValueTask InitializeAsync()
    {
        var connectionString = await StartContainerAsync();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(NullLoggerProvider.Instance);
        });
        services.AddSingleton<TimeProvider>(TimeProvider);
        services.AddBillingServices(
            BillingOptions
                .Create(
                    new ConfigurationBuilder().Build(),
                    _ => CreateEFConfiguration(connectionString)
                )
                .RegisterTestUsage()
        );

        _serviceProvider = services.BuildServiceProvider();
    }

    private async Task<string> StartContainerAsync()
    {
        switch (provider)
        {
            case DatabaseProvider.MsSql:
                var mssql = new MsSqlBuilder().Build();
                _container = mssql;
                await mssql.StartAsync();
                return mssql.GetConnectionString();

            case DatabaseProvider.MySql:
                var mysql = new MySqlBuilder().WithDatabase("corely_billing").Build();
                _container = mysql;
                await mysql.StartAsync();
                return mysql.GetConnectionString();

            default:
                throw new NotSupportedException($"Unsupported provider {provider}.");
        }
    }

    private IEFConfiguration CreateEFConfiguration(string connectionString) =>
        provider switch
        {
            DatabaseProvider.MsSql => new TestMsSqlConfiguration(connectionString),
            DatabaseProvider.MySql => new TestMySqlConfiguration(connectionString),
            _ => throw new NotSupportedException($"Unsupported provider {provider}."),
        };

    public Task MigrateAsync() =>
        WithScopeAsync(services =>
            services.GetRequiredService<BillingDbContext>().Database.MigrateAsync()
        );

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var scope = _serviceProvider!.CreateScope();
        return await work(scope.ServiceProvider);
    }

    public async Task WithScopeAsync(Func<IServiceProvider, Task> work)
    {
        using var scope = _serviceProvider!.CreateScope();
        await work(scope.ServiceProvider);
    }

    public async Task<T> InOperationAsync<T>(
        string idempotencyScope,
        Func<IServiceProvider, Task<T>> work
    )
    {
        using var scope = _serviceProvider!.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IOperationContextAccessor>();
        using var operation = accessor.BeginScope(
            new OperationContext(Guid.CreateVersion7(), idempotencyScope)
        );
        return await work(scope.ServiceProvider);
    }

    internal Task<T> QueryAsync<T>(Func<BillingDbContext, Task<T>> query) =>
        WithScopeAsync(services => query(services.GetRequiredService<BillingDbContext>()));

    public async ValueTask DisposeAsync()
    {
        _serviceProvider?.Dispose();
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
