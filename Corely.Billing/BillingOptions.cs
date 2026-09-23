using Corely.Billing.Telemetry;
using Corely.Billing.Usage;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing;

public class BillingOptions
{
    internal IConfiguration Configuration { get; private set; } = null!;

    internal Func<IServiceProvider, IEFConfiguration>? EFConfigurationFactory { get; private set; }

    internal Func<IServiceProvider, IBillingTelemetry>? TelemetryFactory { get; private set; }

    internal Action<IServiceCollection>? ServiceDecorators { get; private set; }

    internal Dictionary<UsageOperation, string> Operations { get; } = [];

    internal Dictionary<UsageUnit, string> Units { get; } = [];

    private BillingOptions() { }

    public static BillingOptions Create(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new BillingOptions { Configuration = configuration };
    }

    public static BillingOptions Create(
        IConfiguration configuration,
        Func<IServiceProvider, IEFConfiguration> efConfigurationFactory
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(efConfigurationFactory);
        return new BillingOptions
        {
            Configuration = configuration,
            EFConfigurationFactory = efConfigurationFactory,
        };
    }

    public BillingOptions RegisterOperation(string value, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Operations[UsageOperation.From(value)] = displayName;
        return this;
    }

    public BillingOptions RegisterUnit(string value, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Units[UsageUnit.From(value)] = displayName;
        return this;
    }

    public BillingOptions UseTelemetry(Func<IServiceProvider, IBillingTelemetry> telemetryFactory)
    {
        ArgumentNullException.ThrowIfNull(telemetryFactory);
        TelemetryFactory = telemetryFactory;
        return this;
    }

    public BillingOptions DecorateServices(Action<IServiceCollection> decorate)
    {
        ArgumentNullException.ThrowIfNull(decorate);
        ServiceDecorators += decorate;
        return this;
    }
}
