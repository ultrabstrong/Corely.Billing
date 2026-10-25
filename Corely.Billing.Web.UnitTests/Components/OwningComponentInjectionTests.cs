using System.Reflection;
using Corely.Billing.Services;
using Corely.Billing.Web.Components;
using Corely.Billing.Web.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Corely.Billing.Web.UnitTests.Components;

public class OwningComponentInjectionTests
{
    private static readonly Type[] _sharedAcrossTheTab =
    [
        typeof(NavigationManager),
        typeof(IJSRuntime),
    ];

    private static readonly IServiceCollection _services = CreateAppServices();

    private static IServiceCollection CreateAppServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddBillingServices(
            BillingOptions
                .Create(new ConfigurationManager())
                .RegisterUnit("page", "page")
                .RegisterOperation(
                    "extraction",
                    "Extraction",
                    Corely.Billing.Grants.Models.GrantKind.Consumable,
                    "page"
                )
        );
        services.AddBillingWeb<NoAccountAccessor>();
        return services;
    }

    private static List<string> FindScopedInjections(IEnumerable<Type> types) =>
        types
            .Where(t => typeof(OwningComponentBase).IsAssignableFrom(t))
            .SelectMany(t =>
                t.GetProperties(
                        BindingFlags.Instance
                            | BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.DeclaredOnly
                    )
                    .Where(p => p.GetCustomAttribute<InjectAttribute>() != null)
                    .Where(p => !_sharedAcrossTheTab.Contains(p.PropertyType))
                    .Where(p => LifetimeOf(p.PropertyType) == ServiceLifetime.Scoped)
                    .Select(p => $"{t.Name}.{p.Name} ({p.PropertyType.Name})")
            )
            .ToList();

    private static ServiceLifetime? LifetimeOf(Type serviceType) =>
        _services
            .LastOrDefault(d =>
                d.ServiceType == serviceType
                || (
                    serviceType.IsGenericType
                    && d.ServiceType == serviceType.GetGenericTypeDefinition()
                )
            )
            ?.Lifetime;

    private sealed class NoAccountAccessor : IBillingAccountAccessor
    {
        public Task<Guid?> GetAccountIdAsync() => Task.FromResult<Guid?>(null);
    }

    private sealed class ComponentInjectingAScopedService : OwningComponentBase
    {
        [Inject]
        private IGrantService GrantService { get; set; } = null!;
    }

    [Fact]
    public void OwningComponents_InjectNoScopedServices_ForTheBillingWebComponents()
    {
        var types = typeof(BillingComponentBase).Assembly.GetTypes();

        Assert.Contains(types, t => typeof(OwningComponentBase).IsAssignableFrom(t));
        Assert.Empty(FindScopedInjections(types));
    }

    [Fact]
    public void FindScopedInjections_ReportsAScopedService_ForAnOwningComponentThatInjectsOne() =>
        Assert.Single(FindScopedInjections([typeof(ComponentInjectingAScopedService)]));
}
