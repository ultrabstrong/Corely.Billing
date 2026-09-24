using Corely.Billing.Web.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corely.Billing.Web.Extensions;

public static class BillingWebServiceExtensions
{
    public static IServiceCollection AddBillingWeb<TAccountAccessor>(
        this IServiceCollection services
    )
        where TAccountAccessor : class, IBillingAccountAccessor
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IBillingAccountAccessor, TAccountAccessor>();
        services.TryAddScoped<BillingCallGate>();
        return services;
    }
}
