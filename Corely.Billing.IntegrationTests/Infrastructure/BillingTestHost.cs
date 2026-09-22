using Corely.Billing.DataAccess;
using Corely.Billing.Operations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Corely.Billing.IntegrationTests.Infrastructure;

public sealed class BillingTestHost : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _serviceProvider;

    public FakeTimeProvider TimeProvider { get; } =
        new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    public BillingTestHost()
    {
        _connection.Open();

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
                    _ => new SqliteEFConfiguration(_connection)
                )
                .RegisterTestUsage()
        );

        _serviceProvider = services.BuildServiceProvider();

        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.EnsureCreated();
    }

    public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var scope = _serviceProvider.CreateScope();
        return await work(scope.ServiceProvider);
    }

    /// <summary>
    /// Runs <paramref name="work"/> in a fresh DI scope inside the operation context
    /// <paramref name="idempotencyScope"/> names, the way a host runs one unit of work.
    /// </summary>
    public async Task<T> InOperationAsync<T>(
        string idempotencyScope,
        Func<IServiceProvider, Task<T>> work
    )
    {
        using var scope = _serviceProvider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IOperationContextAccessor>();
        using var operation = accessor.BeginScope(
            new OperationContext(Guid.CreateVersion7(), idempotencyScope)
        );
        return await work(scope.ServiceProvider);
    }

    internal Task<T> QueryAsync<T>(Func<BillingDbContext, Task<T>> query) =>
        WithScopeAsync(services => query(services.GetRequiredService<BillingDbContext>()));

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }
}
