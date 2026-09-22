using Corely.Billing.Quota.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corely.Billing.Quota;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddQuotaServices(
        this IServiceCollection services,
        Action<IServiceCollection>? decorate = null
    )
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IQuotaService, QuotaService>();

        decorate?.Invoke(services);

        services.Decorate<IQuotaService, QuotaTelemetryDecorator>();
        services.AddScoped<IGrantSelectionPolicy, ExpiringFirstGrantSelectionPolicy>();
        return services;
    }
}
