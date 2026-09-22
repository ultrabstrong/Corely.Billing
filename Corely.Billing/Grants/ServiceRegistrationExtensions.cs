using Corely.Billing.Grants.DataAccess;
using Corely.Billing.Grants.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Grants;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddEntitlementServices(
        this IServiceCollection services,
        Action<IServiceCollection>? decorate = null
    )
    {
        services.AddScoped<IGrantWriter, GrantWriter>();
        services.AddScoped<IGrantReader, GrantReader>();

        decorate?.Invoke(services);

        services.Decorate<IGrantWriter, GrantWriterTelemetryDecorator>();
        services.Decorate<IGrantReader, GrantReaderTelemetryDecorator>();

        services.AddDbContext<EntitlementsDbContext>();
        return services;
    }
}
