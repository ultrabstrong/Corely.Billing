using Corely.Billing.Consumption.Models;
using Corely.Billing.DataAccess;
using Corely.Billing.Grants.Models;
using Corely.Billing.Services;
using Corely.Billing.Telemetry;
using Corely.Billing.Usage;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Corely.Billing.UnitTests;

public class ServiceRegistrationExtensionsTests
{
    private static ServiceProvider Build(
        BillingOptions options,
        Action<IServiceCollection>? configure = null
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure?.Invoke(services);
        services.AddBillingServices(options);
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
        );
    }

    private static BillingOptions Options(IConfiguration? configuration = null) =>
        BillingOptions.Create(configuration ?? new ConfigurationManager()).RegisterTestUsage();

    [Fact]
    public void AddBillingServices_Throws_ForNoRegisteredOperations() =>
        Assert.Throws<InvalidOperationException>(() =>
            Build(BillingOptions.Create(new ConfigurationManager()).RegisterUnit("page", "page"))
        );

    [Fact]
    public void AddBillingServices_Throws_ForNoRegisteredUnits() =>
        Assert.Throws<InvalidOperationException>(() =>
            Build(BillingOptions.Create(new ConfigurationManager()).RegisterOperation("a", "A"))
        );

    [Fact]
    public void AddBillingServices_ResolvesThePublicServices_ForMockRepos()
    {
        using var provider = Build(Options());
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGrantService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IConsumptionService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IQuotaService>());
    }

    [Fact]
    public void AddBillingServices_RegistersOnlyAKeyedEFConfiguration_ForAnEFConfigurationFactory()
    {
        using var provider = Build(
            BillingOptions
                .Create(new ConfigurationManager(), _ => Mock.Of<IEFConfiguration>())
                .RegisterTestUsage()
        );
        using var scope = provider.CreateScope();

        Assert.NotNull(
            scope.ServiceProvider.GetKeyedService<IEFConfiguration>(EFConfigurationKeys.BILLING)
        );
        Assert.Null(scope.ServiceProvider.GetService<IEFConfiguration>());
    }

    [Fact]
    public void BillingDbContext_IgnoresTheHostsUnkeyedEFConfiguration_ForAHostThatRegisteredOne()
    {
        var billingConfiguration = new Mock<EFInMemoryConfigurationBase> { CallBase = true };
        billingConfiguration
            .Setup(c => c.Configure(It.IsAny<DbContextOptionsBuilder>()))
            .Callback<DbContextOptionsBuilder>(b =>
                b.UseInMemoryDatabase(Guid.NewGuid().ToString())
            );
        var decoyConfiguration = new Mock<IEFConfiguration>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBillingServices(
            BillingOptions
                .Create(new ConfigurationManager(), _ => billingConfiguration.Object)
                .RegisterTestUsage()
        );
        services.AddScoped(_ => decoyConfiguration.Object);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        _ = scope.ServiceProvider.GetRequiredService<BillingDbContext>().Database.ProviderName;

        billingConfiguration.Verify(
            c => c.Configure(It.IsAny<DbContextOptionsBuilder>()),
            Times.Once
        );
        decoyConfiguration.Verify(
            c => c.Configure(It.IsAny<DbContextOptionsBuilder>()),
            Times.Never
        );
    }

    [Fact]
    public void AddBillingServices_RegistersTheVocabulary_ForTheRegisteredUsage()
    {
        using var provider = Build(Options());

        var vocabulary = provider.GetRequiredService<IUsageVocabulary>();

        Assert.True(vocabulary.Knows(TestUsage.Extraction));
        Assert.True(vocabulary.Knows(TestUsage.Page));
        Assert.Equal("No-op", vocabulary.DisplayName(TestUsage.NoOp));
    }

    [Fact]
    public void AddBillingServices_UsesANoOpTelemetry_ForNoTelemetryFactory()
    {
        using var provider = Build(Options());

        Assert.IsType<NullBillingTelemetry>(provider.GetRequiredService<IBillingTelemetry>());
    }

    [Fact]
    public void AddBillingServices_UsesTheHostsTelemetry_ForATelemetryFactory()
    {
        var telemetry = Mock.Of<IBillingTelemetry>();

        using var provider = Build(Options().UseTelemetry(_ => telemetry));

        Assert.Same(telemetry, provider.GetRequiredService<IBillingTelemetry>());
    }

    [Fact]
    public void AddBillingServices_KeepsTheHostsTimeProvider_ForAHostThatRegisteredOne()
    {
        var time = new FakeTimeProvider();

        using var provider = Build(
            Options(),
            services => services.AddSingleton<TimeProvider>(time)
        );

        Assert.Same(time, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddBillingServices_BindsReservationOptions_ForTheConfiguredSection()
    {
        var configuration = new ConfigurationManager();
        configuration["ReservationOptions:ReservationTtl"] = "02:00:00";

        using var provider = Build(Options(configuration));

        Assert.Equal(
            TimeSpan.FromHours(2),
            provider.GetRequiredService<IOptions<ReservationOptions>>().Value.ReservationTtl
        );
    }

    [Fact]
    public async Task AddBillingServices_AppliesTheHostsDecorator_ForDecorateServices()
    {
        var hostDecorator = new Mock<IGrantService>();
        hostDecorator
            .Setup(s => s.DeleteGrantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), default))
            .ReturnsAsync(new DeleteGrantResult(DeleteGrantResultCode.UnauthorizedError, "denied"));

        using var provider = Build(
            Options()
                .DecorateServices(services =>
                    services.Decorate<IGrantService>((_, _) => hostDecorator.Object)
                )
        );
        using var scope = provider.CreateScope();

        var result = await scope
            .ServiceProvider.GetRequiredService<IGrantService>()
            .DeleteGrantAsync(Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.Equal(DeleteGrantResultCode.UnauthorizedError, result.ResultCode);
        Assert.IsNotType(
            hostDecorator.Object.GetType(),
            scope.ServiceProvider.GetRequiredService<IGrantService>()
        );
    }
}
