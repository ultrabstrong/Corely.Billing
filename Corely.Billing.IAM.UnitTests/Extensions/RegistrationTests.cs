using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.IAM.Extensions;
using Corely.Billing.IAM.UnitTests.Auditing;
using Corely.Billing.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.IAM;
using Corely.IAM.Audits.Providers;
using Corely.IAM.Permissions.Providers;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Providers;
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
            .RegisterUnit(Page.Value, "page")
            .RegisterOperation(Extraction.Value, "Extraction", GrantKind.Consumable, Page.Value);

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
            BillingResourceTypes.GRANT_REQUEST_DESCRIPTION,
            registry.Get(BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE)?.Description
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

    [Theory]
    [InlineData(BillingResourceTypes.GRANT_RESOURCE_TYPE, new[] { AuthAction.Read })]
    [InlineData(
        BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE,
        new[] { AuthAction.Read, AuthAction.Create }
    )]
    [InlineData(BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE, new[] { AuthAction.Read })]
    [InlineData(
        BillingResourceTypes.QUOTA_RESOURCE_TYPE,
        new[] { AuthAction.Read, AuthAction.Execute }
    )]
    public void RegisterBillingResourceTypes_GivesOwnersOnlyTheseActions_ForEachType(
        string resourceType,
        AuthAction[] ownerActions
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIAMServices(
            IAMOptions
                .Create(Configuration, Mock.Of<ISecurityConfigurationProvider>())
                .RegisterBillingResourceTypes()
        );

        var registry = services.BuildServiceProvider().GetRequiredService<IResourceTypeRegistry>();

        Assert.Equal(ownerActions, registry.Get(resourceType)!.OwnerActions);
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
        services.AddScoped(_ => Mock.Of<IUserContextProvider>());
        services.AddScoped<IAuditProvider, CapturingAuditProvider>();
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
                            GrantKind.Consumable,
                            1,
                            DateTime.UtcNow,
                            DateTime.UtcNow.AddDays(1)
                        )
                    )
            ).ResultCode
        );
        Assert.Equal(
            RequestGrantResultCode.UnauthorizedError,
            (
                await provider
                    .GetRequiredService<IGrantRequestService>()
                    .RequestGrantAsync(new(AccountId, Extraction, 1))
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
            QuotaReadResultCode.UnauthorizedError,
            (
                await provider
                    .GetRequiredService<IQuotaService>()
                    .GetBalanceAsync(AccountId, Extraction)
            ).ResultCode
        );
        Assert.Equal(
            RetrieveResultCode.UnauthorizedError,
            (
                await provider
                    .GetRequiredService<IQuotaService>()
                    .ListRegistrationsAsync(new([AccountId]))
            ).ResultCode
        );
    }
}
