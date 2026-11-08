using System.Reflection;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Services;
using Corely.Billing.Web.Components;
using Corely.Billing.Web.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Corely.Billing.Web.UnitTests.Components;

public class ComponentInjectionTests
{
    // Shared across the tab on purpose, and never reaching the database themselves.
    private static readonly Type[] _sharedAcrossTheTab =
    [
        typeof(IBillingOperationScopes),
        typeof(IGrantActionGate),
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
                .RegisterOperation("extraction", "Extraction", GrantKind.Consumable, "page")
        );
        services.AddBillingWeb<NoAccountAccessor>();
        return services;
    }

    private static IEnumerable<Type> Components(IEnumerable<Type> types) =>
        types.Where(t => typeof(IComponent).IsAssignableFrom(t));

    private static List<string> FindScopedInjections(IEnumerable<Type> types) =>
        Components(types)
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

    private static Type[] WebComponents() => typeof(BillingComponentBase).Assembly.GetTypes();

    private sealed class NoAccountAccessor : IBillingAccountAccessor
    {
        public Task<Guid?> GetAccountIdAsync() => Task.FromResult<Guid?>(null);
    }

    private sealed class ComponentInjectingAScopedService : ComponentBase
    {
        [Inject]
        private IGrantService GrantService { get; set; } = null!;
    }

    [Fact]
    public void Components_InjectNoScopedServices_ForTheBillingWebComponents()
    {
        var types = WebComponents();

        Assert.Contains(types, t => typeof(BillingOperationComponentBase).IsAssignableFrom(t));
        Assert.Empty(FindScopedInjections(types));
    }

    [Fact]
    public void Components_OwnNoScopeForTheirLifetime_ForTheBillingWebComponents() =>
        Assert.Empty(
            Components(WebComponents())
                .Where(t => typeof(OwningComponentBase).IsAssignableFrom(t))
                .Select(t => t.Name)
        );

    [Fact]
    public void FindScopedInjections_ReportsAScopedService_ForAnyComponentThatInjectsOne() =>
        Assert.Single(FindScopedInjections([typeof(ComponentInjectingAScopedService)]));
}
