using Corely.Billing.Grants.Models;
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

    internal Dictionary<UsageOperation, UsageOperationDefinition> Operations { get; } = [];

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

    public BillingOptions RegisterOperation(
        string value,
        string displayName,
        GrantKind kind,
        string? unit = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var operation = UsageOperation.From(value);
        Operations[operation] = new UsageOperationDefinition(
            operation,
            displayName,
            kind,
            unit is null ? null : UsageUnit.From(unit)
        );
        return this;
    }

    public BillingOptions RegisterUnit(string value, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        Units[UsageUnit.From(value)] = displayName;
        return this;
    }

    internal string? DeclarationProblem() =>
        Operations.Count == 0
            ? "Register at least one operation on BillingOptions. Nothing can be granted or "
                + "consumed without one."
        : Operations.Values.FirstOrDefault(o => o.Unit is { } unit && !Units.ContainsKey(unit))
            is { } unregistered
            ? $"Operation '{unregistered.Operation}' names unit '{unregistered.Unit}', which is "
                + "not registered. Register it with RegisterUnit."
        : Operations.Values.FirstOrDefault(o => (o.Kind == GrantKind.Feature) != (o.Unit is null))
            is { } misdeclared
            ? misdeclared.Kind == GrantKind.Feature
                    ? $"Operation '{misdeclared.Operation}' is a feature and names a unit. A feature "
                        + "is on or off and has none."
                : $"Operation '{misdeclared.Operation}' is {misdeclared.Kind} and names no unit. "
                    + "Every kind but a feature is counted in a unit."
        : null;

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
