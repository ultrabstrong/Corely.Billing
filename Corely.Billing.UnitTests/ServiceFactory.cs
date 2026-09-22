using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Corely.Billing.UnitTests;

public class ServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    public FakeTimeProvider TimeProvider { get; } =
        new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    public ServiceFactory()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(NullLoggerProvider.Instance);
        });

        services.AddSingleton<TimeProvider>(TimeProvider);
        services.AddBillingServices(
            BillingOptions.Create(new ConfigurationManager()).RegisterTestUsage()
        );

        _serviceProvider = services.BuildServiceProvider();
    }

    public T GetRequiredService<T>()
        where T : notnull => _serviceProvider.GetRequiredService<T>();
}
