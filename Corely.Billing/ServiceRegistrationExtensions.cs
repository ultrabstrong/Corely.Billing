using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.DataAccess;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Operations;
using Corely.Billing.Quota.Processors;
using Corely.Billing.Services;
using Corely.Billing.Telemetry;
using Corely.Billing.Usage;
using Corely.Billing.Validators;
using Corely.Billing.Validators.FluentValidators;
using Corely.DataAccess.Extensions;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corely.Billing;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddBillingServices(
        this IServiceCollection serviceCollection,
        BillingOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Operations.Count == 0 || options.Units.Count == 0)
        {
            throw new InvalidOperationException(
                "Register at least one operation and one unit on BillingOptions. Nothing can be "
                    + "granted or consumed without them."
            );
        }

        if (options.EFConfigurationFactory != null)
        {
            var efConfigurationFactory = options.EFConfigurationFactory;
            serviceCollection.AddKeyedScoped(
                EFConfigurationKeys.BILLING,
                (sp, _) => efConfigurationFactory(sp)
            );
            serviceCollection.AddDbContext<BillingDbContext>();
            serviceCollection.RegisterEntityFrameworkReposAndUoW();
        }
        else
        {
            serviceCollection.RegisterMockReposAndUoW();
        }

        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.TryAddSingleton<
            IOperationContextAccessor,
            AsyncLocalOperationContextAccessor
        >();
        serviceCollection.AddSingleton<IUsageVocabulary>(
            new UsageVocabulary(
                [.. options.Operations.Select(o => new UsageOperationDefinition(o.Key, o.Value))],
                [.. options.Units.Select(u => new UsageUnitDefinition(u.Key, u.Value))]
            )
        );

        if (options.TelemetryFactory != null)
            serviceCollection.AddSingleton(options.TelemetryFactory);
        else
            serviceCollection.AddSingleton<IBillingTelemetry, NullBillingTelemetry>();

        serviceCollection.Configure<ReservationOptions>(
            options.Configuration.GetSection(ReservationOptions.NAME)
        );

        serviceCollection.AddValidatorsFromAssemblyContaining<FluentValidationProvider>(
            includeInternalTypes: true
        );
        serviceCollection.AddScoped<IFluentValidatorFactory, FluentValidatorFactory>();
        serviceCollection.AddScoped<IValidationProvider, FluentValidationProvider>();

        serviceCollection.AddScoped<IGrantSelectionPolicy, ExpiringFirstGrantSelectionPolicy>();

        serviceCollection.AddScoped<IGrantProcessor, GrantProcessor>();
        serviceCollection.Decorate<IGrantProcessor, GrantProcessorTelemetryDecorator>();

        serviceCollection.AddScoped<IConsumptionProcessor, ConsumptionProcessor>();
        serviceCollection.Decorate<IConsumptionProcessor, ConsumptionProcessorTelemetryDecorator>();

        serviceCollection.AddScoped<IConsumptionReportProcessor, ConsumptionReportProcessor>();
        serviceCollection.Decorate<
            IConsumptionReportProcessor,
            ConsumptionReportProcessorTelemetryDecorator
        >();

        serviceCollection.AddScoped<IQuotaProcessor, QuotaProcessor>();
        serviceCollection.Decorate<IQuotaProcessor, QuotaProcessorTelemetryDecorator>();

        serviceCollection.AddScoped<IGrantService, GrantService>();
        serviceCollection.AddScoped<IConsumptionService, ConsumptionService>();
        serviceCollection.AddScoped<IQuotaService, QuotaService>();

        options.ServiceDecorators?.Invoke(serviceCollection);

        serviceCollection.Decorate<IGrantService, GrantServiceTelemetryDecorator>();
        serviceCollection.Decorate<IConsumptionService, ConsumptionServiceTelemetryDecorator>();
        serviceCollection.Decorate<IQuotaService, QuotaServiceTelemetryDecorator>();

        return serviceCollection;
    }
}
