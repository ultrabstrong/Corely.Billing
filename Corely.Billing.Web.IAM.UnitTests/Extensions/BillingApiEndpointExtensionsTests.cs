using Corely.Billing.Operations;
using Corely.Billing.Services;
using Corely.Billing.Web.IAM.Extensions;
using Corely.IAM.Users.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.IAM.UnitTests.Extensions;

public class BillingApiEndpointExtensionsTests
{
    private static readonly string[] MeteringRoutes = ["/reserve", "/settle", "/release"];

    [Fact]
    public void MapBillingApiEndpoints_MapsNoMeteringWrite_ForAHost()
    {
        var routes = Routes(app => app.MapBillingApiEndpoints());

        Assert.DoesNotContain(routes, r => MeteringRoutes.Any(r.EndsWith));
        Assert.Contains(routes, r => r.EndsWith("/balance"));
        Assert.Contains(routes, r => r.EndsWith("/registrations"));
    }

    [Fact]
    public void MapBillingMeteringEndpoints_MapsOnlyTheMeteringWrites_ForAHost()
    {
        var routes = Routes(app => app.MapBillingMeteringEndpoints());

        Assert.Equal(3, routes.Count);
        Assert.All(routes, r => Assert.Contains(MeteringRoutes, r.EndsWith));
    }

    private static List<string> Routes(Action<WebApplication> map)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IUserContextProvider>());
        builder.Services.AddSingleton(Mock.Of<IQuotaService>());
        builder.Services.AddSingleton(Mock.Of<IGrantService>());
        builder.Services.AddSingleton(Mock.Of<IConsumptionService>());
        builder.Services.AddSingleton(Mock.Of<IOperationContextAccessor>());
        var app = builder.Build();
        map(app);

        return
        [
            .. ((IEndpointRouteBuilder)app)
                .DataSources.SelectMany(d => d.Endpoints)
                .OfType<RouteEndpoint>()
                .Select(e => e.RoutePattern.RawText!),
        ];
    }
}
