using Corely.Billing;
using Corely.Billing.Consumption.DataAccess;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corely.Billing.Consumption;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddMeteringServices(
        this IServiceCollection services,
        Action<IServiceCollection>? decorate = null
    )
    {
        // Registered here rather than left to each host: the writer cannot record anything without
        // it, and a host that forgot the line would only find out when a charge went missing.
        // Opening a scope per unit of work is still the host's job.
        services.AddOperationContext();

        services.AddOptions<MeteringOptions>().BindConfiguration(MeteringOptions.SectionName);
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IConsumptionWriter, ConsumptionWriter>();
        services.AddScoped<IConsumptionReader, ConsumptionReader>();

        decorate?.Invoke(services);

        services.Decorate<IConsumptionWriter, ConsumptionWriterTelemetryDecorator>();
        services.Decorate<IConsumptionReader, ConsumptionReaderTelemetryDecorator>();

        services.AddDbContext<MeteringDbContext>();
        return services;
    }
}
