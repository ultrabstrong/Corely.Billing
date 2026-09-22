using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corely.Billing;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddUsageVocabulary(
        this IServiceCollection services,
        Action<UsageVocabularyBuilder> configure
    )
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new UsageVocabularyBuilder();
        configure(builder);
        services.AddSingleton(builder.Build());

        return services;
    }

    public static IServiceCollection AddOperationContext(this IServiceCollection services)
    {
        services.TryAddSingleton<IOperationContextAccessor, AsyncLocalOperationContextAccessor>();
        return services;
    }
}
