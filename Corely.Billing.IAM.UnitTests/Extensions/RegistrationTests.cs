using Corely.Billing.Grants.Models;
using Corely.Billing.IAM.Extensions;
using Corely.Billing.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.IAM;
using Corely.IAM.Permissions.Providers;
using Corely.IAM.Security.Constants;
using Corely.IAM.Security.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Corely.Billing.IAM.UnitTests.TestUsage;

namespace Corely.Billing.IAM.UnitTests.Extensions;

public class RegistrationTests
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder().Build();

    private static BillingOptions Billing() =>
        BillingOptions
            .Create(Configuration)
            .RegisterOperation(Extraction.Value, "Extraction")
            .RegisterUnit(Page.Value, "page");

    [Fact]
    public void RegisterBillingResourceTypes_RegistersEveryType_WithIam()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIAMServices(
            IAMOptions
                .Create(Configuration, Mock.Of<ISecurityConfigurationProvider>())
                .RegisterBillingResourceTypes()
        );

        var registry = services.BuildServiceProvider().GetRequiredService<IResourceTypeRegistry>();

        Assert.Equal(
            BillingResourceTypes.GRANT_DESCRIPTION,
            registry.Get(BillingResourceTypes.GRANT_RESOURCE_TYPE)?.Description
        );
        Assert.Equal(
            BillingResourceTypes.CONSUMPTION_DESCRIPTION,
            registry.Get(BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE)?.Description
        );
        Assert.Equal(
            BillingResourceTypes.QUOTA_DESCRIPTION,
            registry.Get(BillingResourceTypes.QUOTA_RESOURCE_TYPE)?.Description
        );
    }

    [Fact]
    public void UseCorelyIamPermissions_Throws_ForAHostWithoutIam()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddBillingServices(Billing().UseCorelyIamPermissions())
        );

        Assert.Contains("AddIAMServices", ex.Message);
    }

    [Fact]
    public async Task UseCorelyIamPermissions_ChecksPermissions_ForEveryBillingService()
    {
        var authorization = new Mock<IAuthorizationProvider>();
        authorization.Setup(p => p.HasAccountContext(It.IsAny<Guid>())).Returns(true);
        authorization
            .Setup(p =>
                p.IsAuthorizedAsync(It.IsAny<AuthAction>(), It.IsAny<string>(), It.IsAny<Guid[]>())
            )
            .ReturnsAsync(false);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => authorization.Object);
        services.AddBillingServices(Billing().UseCorelyIamPermissions());

        using var scope = services.BuildServiceProvider().CreateScope();
        var provider = scope.ServiceProvider;

        Assert.Equal(
            CreateGrantResultCode.UnauthorizedError,
            (
                await provider
                    .GetRequiredService<IGrantService>()
                    .CreateGrantAsync(
                        new(
                            AccountId,
                            Extraction,
                            Page,
                            1,
                            DateTime.UtcNow,
                            DateTime.UtcNow.AddDays(1)
                        )
                    )
            ).ResultCode
        );
        Assert.Equal(
            RetrieveResultCode.UnauthorizedError,
            (
                await provider
                    .GetRequiredService<IConsumptionService>()
                    .ListProvidersAsync(AccountId)
            ).ResultCode
        );
        Assert.Equal(
            QuotaAvailability.Unauthorized,
            await provider
                .GetRequiredService<IQuotaService>()
                .GetAvailabilityAsync(AccountId, Extraction, Page)
        );
    }
}
