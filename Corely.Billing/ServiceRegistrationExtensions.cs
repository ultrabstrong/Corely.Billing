using Corely.Billing.Accounts.Processors;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.DataAccess;
using Corely.Billing.GrantRequests.Processors;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Operations;
using Corely.Billing.Prices.Models;
using Corely.Billing.Prices.Processors;
using Corely.Billing.Quota.Processors;
using Corely.Billing.Registrations.Processors;
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
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddBillingServices(BillingOptions options)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(options);

            if (options.DeclarationProblem() is { } problem)
                throw new InvalidOperationException(problem);

            var priceSection = options.Configuration.GetSection(PriceOptions.NAME);
            if (
                options.PriceProblem(priceSection.Get<PriceOptions>()?.Prices ?? []) is
                { } priceProblem
            )
            {
                throw new InvalidOperationException(priceProblem);
            }

            if (options.EFConfigurationFactory != null)
            {
                var efConfigurationFactory = options.EFConfigurationFactory;
                serviceCollection.AddKeyedScoped(
                    EFConfigurationKeys.BILLING,
                    (sp, _) => efConfigurationFactory(sp)
                );
                serviceCollection.AddKeyedSingleton(
                    EFConfigurationKeys.BILLING,
                    new BillingCompiledModel(options.CompiledModel)
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
                new UsageVocabulary([.. options.Operations.Values], [.. options.Units.Values])
            );

            if (options.TelemetryFactory != null)
                serviceCollection.AddSingleton(options.TelemetryFactory);
            else
                serviceCollection.AddSingleton<IBillingTelemetry, NullBillingTelemetry>();

            serviceCollection.Configure<ReservationOptions>(
                options.Configuration.GetSection(ReservationOptions.NAME)
            );
            serviceCollection.Configure<PriceOptions>(priceSection);
            serviceCollection.AddSingleton(new PriceSourceSelection(options.PriceSource));

            serviceCollection.AddValidatorsFromAssemblyContaining<FluentValidationProvider>(
                includeInternalTypes: true
            );
            serviceCollection.AddScoped<IFluentValidatorFactory, FluentValidatorFactory>();
            serviceCollection.AddScoped<IValidationProvider, FluentValidationProvider>();

            serviceCollection.AddScoped<IGrantSelectionPolicy, ExpiringFirstGrantSelectionPolicy>();

            serviceCollection.AddScoped<
                IRegistrationSelectionPolicy,
                RegistrationSelectionPolicy
            >();
            serviceCollection.AddScoped<IRegistrationProcessor, RegistrationProcessor>();
            serviceCollection.Decorate<
                IRegistrationProcessor,
                RegistrationProcessorTelemetryDecorator
            >();

            serviceCollection.AddScoped<IUsagePriceProcessor, UsagePriceProcessor>();
            serviceCollection.Decorate<
                IUsagePriceProcessor,
                UsagePriceProcessorTelemetryDecorator
            >();

            serviceCollection.AddScoped<IGrantProcessor, GrantProcessor>();
            serviceCollection.Decorate<IGrantProcessor, GrantProcessorTelemetryDecorator>();

            serviceCollection.AddScoped<IGrantRequestProcessor, GrantRequestProcessor>();
            serviceCollection.Decorate<
                IGrantRequestProcessor,
                GrantRequestProcessorTelemetryDecorator
            >();

            serviceCollection.AddScoped<IConsumptionProcessor, ConsumptionProcessor>();
            serviceCollection.Decorate<
                IConsumptionProcessor,
                ConsumptionProcessorTelemetryDecorator
            >();

            serviceCollection.AddScoped<IConsumptionReportProcessor, ConsumptionReportProcessor>();
            serviceCollection.Decorate<
                IConsumptionReportProcessor,
                ConsumptionReportProcessorTelemetryDecorator
            >();

            serviceCollection.AddScoped<IQuotaProcessor, QuotaProcessor>();
            serviceCollection.Decorate<IQuotaProcessor, QuotaProcessorTelemetryDecorator>();

            serviceCollection.AddScoped<IAccountDataProcessor, AccountDataProcessor>();
            serviceCollection.Decorate<
                IAccountDataProcessor,
                AccountDataProcessorTelemetryDecorator
            >();

            serviceCollection.AddScoped<IGrantService, GrantService>();
            serviceCollection.AddScoped<IGrantRequestService, GrantRequestService>();
            serviceCollection.AddScoped<IConsumptionService, ConsumptionService>();
            serviceCollection.AddScoped<IQuotaService, QuotaService>();
            serviceCollection.AddScoped<IUsagePriceService, UsagePriceService>();
            serviceCollection.AddScoped<IAccountDataService, AccountDataService>();

            options.ServiceDecorators?.Invoke(serviceCollection);

            serviceCollection.Decorate<IGrantService, GrantServiceTelemetryDecorator>();
            serviceCollection.Decorate<
                IGrantRequestService,
                GrantRequestServiceTelemetryDecorator
            >();
            serviceCollection.Decorate<IConsumptionService, ConsumptionServiceTelemetryDecorator>();
            serviceCollection.Decorate<IQuotaService, QuotaServiceTelemetryDecorator>();
            serviceCollection.Decorate<IUsagePriceService, UsagePriceServiceTelemetryDecorator>();
            serviceCollection.Decorate<IAccountDataService, AccountDataServiceTelemetryDecorator>();

            return serviceCollection;
        }
    }
}
