using System.Reflection;
using Corely.Billing.Grants.Models;
using Corely.Billing.IAM.Extensions;
using Corely.Billing.Services;
using Corely.IAM.Audits.Providers;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Corely.Billing.IAM.UnitTests.TestUsage;

namespace Corely.Billing.IAM.UnitTests.Auditing;

public class AuditDecoratorCoverageTests
{
    private static readonly IConfiguration Configuration = new ConfigurationBuilder().Build();

    public static TheoryData<Type> ServiceInterfaces() =>
        [
            .. typeof(IGrantService)
                .Assembly.GetExportedTypes()
                .Where(t =>
                    t.IsInterface
                    && t.Namespace == typeof(IGrantService).Namespace
                    && t.Name.StartsWith('I')
                    && t.Name.EndsWith("Service")
                )
                .OrderBy(t => t.Name),
        ];

    [Fact]
    public void ServiceInterfaces_AreFound()
    {
        var found = ServiceInterfaces().Select(r => r.Data).ToList();

        Assert.Contains(typeof(IGrantService), found);
        Assert.Contains(typeof(IConsumptionService), found);
        Assert.Contains(typeof(IQuotaService), found);
    }

    [Theory]
    [MemberData(nameof(ServiceInterfaces))]
    public void EveryService_RunsTelemetryThenAuditThenAuthorizationThenTheService(
        Type serviceInterface
    )
    {
        var chain = ResolveChain(serviceInterface).Select(t => t.Name).ToList();

        var telemetry = chain.FindIndex(n => n.EndsWith("TelemetryDecorator"));
        var audit = chain.FindIndex(n => n.EndsWith("AuditDecorator"));
        var authorization = chain.FindIndex(n => n.EndsWith("AuthorizationDecorator"));

        Assert.True(audit >= 0, $"{serviceInterface.Name} has no audit decorator");
        Assert.True(telemetry < audit, $"{serviceInterface.Name}: telemetry must wrap audit");
        Assert.True(
            audit < authorization,
            $"{serviceInterface.Name}: audit must wrap authorization"
        );
        Assert.DoesNotContain("Decorator", chain[^1]);
    }

    [Fact]
    public async Task ARefusedCall_IsStillRecorded()
    {
        var auditProvider = new CapturingAuditProvider();
        using var scope = BuildServices(auditProvider).CreateScope();

        var result = await scope
            .ServiceProvider.GetRequiredService<IGrantService>()
            .DeleteGrantAsync(AccountId, GrantId);

        Assert.Equal(DeleteGrantResultCode.UnauthorizedError, result.ResultCode);
        var captured = Assert.Single(auditProvider.Calls);
        Assert.Equal(nameof(IGrantService.DeleteGrantAsync), captured.Operation);
        Assert.Equal(nameof(DeleteGrantResultCode.UnauthorizedError), captured.Outcome!.ResultCode);
    }

    private static ServiceProvider BuildServices(IAuditProvider auditProvider)
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
        services.AddScoped(_ => auditProvider);
        services.AddBillingServices(
            BillingOptions
                .Create(Configuration)
                .RegisterUnit(Page.Value, "page")
                .RegisterOperation(Extraction.Value, "Extraction", GrantKind.Consumable, Page.Value)
                .UseCorelyIamPermissions()
        );
        return services.BuildServiceProvider();
    }

    private static List<Type> ResolveChain(Type serviceInterface)
    {
        using var scope = BuildServices(new CapturingAuditProvider()).CreateScope();
        object? current = scope.ServiceProvider.GetRequiredService(serviceInterface);
        var chain = new List<Type>();
        while (current is not null)
        {
            chain.Add(current.GetType());
            current = current
                .GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(f => f.FieldType == serviceInterface)
                ?.GetValue(current);
        }
        return chain;
    }
}
