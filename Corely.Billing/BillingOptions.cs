using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Telemetry;
using Corely.Billing.Usage;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing;

public class BillingOptions
{
    internal IConfiguration Configuration { get; private set; } = null!;

    internal Func<IServiceProvider, IEFConfiguration>? EFConfigurationFactory { get; private set; }

    internal IModel? CompiledModel { get; private set; }

    internal Func<IServiceProvider, IBillingTelemetry>? TelemetryFactory { get; private set; }

    internal Action<IServiceCollection>? ServiceDecorators { get; private set; }

    internal Dictionary<UsageOperation, UsageOperationDefinition> Operations { get; } = [];

    internal Dictionary<UsageUnit, UsageUnitDefinition> Units { get; } = [];

    internal PriceSource PriceSource { get; private set; } = PriceSource.Configuration;

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

    public BillingOptions RegisterUnit(string value, string displayName, int decimals = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            decimals,
            UsageUnitDefinition.DECIMALS_MAX_VALUE
        );
        var unit = UsageUnit.From(value);
        Units[unit] = new UsageUnitDefinition(unit, displayName, decimals);
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

    public BillingOptions UsePlatformPrices()
    {
        PriceSource = PriceSource.Platform;
        return this;
    }

    internal string? PriceProblem(IReadOnlyDictionary<string, ConfiguredPrice> configured)
    {
        if (configured.Count == 0)
            return null;
        if (PriceSource == PriceSource.Platform)
            return $"{PriceOptions.NAME}:Prices is set, but this host's prices are platform "
                + "settings (UsePlatformPrices), so configuration would be ignored. Remove it.";

        foreach (var (operation, price) in configured)
        {
            if (
                !Operations.TryGetValue(UsageOperation.From(operation), out var definition)
                || definition.Kind != GrantKind.Consumable
            )
            {
                return $"{PriceOptions.NAME}:Prices names '{operation}', which is not a "
                    + "registered consumable operation. Only a consumable operation has a price "
                    + "per unit.";
            }
            if (price.ToMoney().Problem() is { } problem)
                return $"{PriceOptions.NAME}:Prices:{operation}: {problem}";
        }
        return null;
    }

    public BillingOptions UseCompiledModel(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        CompiledModel = model;
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
