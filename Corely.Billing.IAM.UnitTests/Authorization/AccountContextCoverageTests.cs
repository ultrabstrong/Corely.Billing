using Corely.Billing.Consumption.Enums;
using Corely.Billing.Consumption.Models;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.IAM.Extensions;
using Corely.Billing.IAM.UnitTests.Auditing;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Services;
using Corely.IAM.Audits.Providers;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Corely.Billing.IAM.UnitTests.TestUsage;

namespace Corely.Billing.IAM.UnitTests.Authorization;

public class AccountContextCoverageTests
{
    private static readonly Guid Victim = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Attacker = AccountId;
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Dictionary<string, Func<IServiceProvider, Task<object?>>> Cases = new()
    {
        ["IGrantService.CreateGrantAsync"] = s =>
            Grant(s)
                .CreateGrantAsync(
                    new CreateGrantRequest(
                        Victim,
                        Extraction,
                        GrantKind.Consumable,
                        5,
                        Now,
                        Now.AddDays(1)
                    )
                )
                .AsResult(),
        ["IGrantService.GetGrantAsync"] = s => Grant(s).GetGrantAsync(Victim, GrantId).AsResult(),
        ["IGrantService.ListGrantsAsync"] = s =>
            Grant(s).ListGrantsAsync(new ListGrantsRequest([Attacker, Victim])).AsResult(),
        ["IGrantService.UpdateGrantAsync"] = s =>
            Grant(s)
                .UpdateGrantAsync(new UpdateGrantRequest(Victim, GrantId, 1, Now, Now.AddDays(1)))
                .AsResult(),
        ["IGrantService.SetGrantDisabledAsync"] = s =>
            Grant(s)
                .SetGrantDisabledAsync(new SetGrantDisabledRequest(Victim, GrantId, true))
                .AsResult(),
        ["IGrantService.DeleteGrantAsync"] = s =>
            Grant(s).DeleteGrantAsync(Victim, GrantId).AsResult(),
        ["IGrantRequestService.RequestGrantAsync"] = s =>
            Request(s).RequestGrantAsync(new RequestGrantRequest(Victim, Extraction, 5)).AsResult(),
        ["IGrantRequestService.GetGrantRequestAsync"] = s =>
            Request(s).GetGrantRequestAsync(Victim, GrantId).AsResult(),
        ["IGrantRequestService.ListGrantRequestsAsync"] = s =>
            Request(s).ListGrantRequestsAsync(new ListGrantRequestsRequest([Victim])).AsResult(),
        ["IGrantRequestService.ApproveGrantRequestAsync"] = s =>
            Request(s)
                .ApproveGrantRequestAsync(
                    new ApproveGrantRequestRequest(Victim, GrantId, Now, Now.AddDays(30))
                )
                .AsResult(),
        ["IGrantRequestService.DeclineGrantRequestAsync"] = s =>
            Request(s)
                .DeclineGrantRequestAsync(new DeclineGrantRequestRequest(Victim, GrantId))
                .AsResult(),
        ["IQuotaService.GetBalanceAsync"] = s =>
            Quota(s).GetBalanceAsync(Victim, Extraction).AsResult(),
        ["IQuotaService.ReserveAsync"] = s =>
            Quota(s)
                .ReserveAsync(new ReserveQuotaRequest(Victim, Extraction, Page, 1, "provider"))
                .AsResult(),
        ["IQuotaService.SettleAsync"] = s =>
            Quota(s).SettleAsync(new SettleQuotaRequest(Victim, Extraction, Page, 1)).AsResult(),
        ["IQuotaService.ReleaseAsync"] = s =>
            Quota(s).ReleaseAsync(new ReleaseQuotaRequest(Victim, Extraction, Page)).AsResult(),
        ["IQuotaService.GetAllowanceAsync"] = s =>
            Quota(s).GetAllowanceAsync(Victim, Extraction).AsResult(),
        ["IQuotaService.HasFeatureAsync"] = s =>
            Quota(s).HasFeatureAsync(Victim, Extraction).AsResult(),
        ["IQuotaService.GetCapacityAsync"] = s =>
            Quota(s).GetCapacityAsync(Victim, Extraction).AsResult(),
        ["IQuotaService.RegisterAsync"] = s =>
            Quota(s).RegisterAsync(new RegisterRequest(Victim, Extraction)).AsResult(),
        ["IQuotaService.ReleaseRegistrationAsync"] = s =>
            Quota(s).ReleaseRegistrationAsync(Victim, GrantId).AsResult(),
        ["IQuotaService.MoveRegistrationsAsync"] = s =>
            Quota(s)
                .MoveRegistrationsAsync(new MoveRegistrationsRequest(Victim, GrantId, GrantId))
                .AsResult(),
        ["IQuotaService.ListRegistrationsAsync"] = s =>
            Quota(s).ListRegistrationsAsync(new ListRegistrationsRequest([Victim])).AsResult(),
        ["IConsumptionService.GetConsumptionTotalAsync"] = s =>
            Consumption(s)
                .GetConsumptionTotalAsync(new GetConsumptionTotalRequest(Victim, Extraction, Page))
                .AsResult(),
        ["IConsumptionService.GetConsumptionTimeSeriesAsync"] = s =>
            Consumption(s)
                .GetConsumptionTimeSeriesAsync(
                    new GetConsumptionTimeSeriesRequest(
                        Victim,
                        Now.AddDays(-1),
                        Now,
                        TimeBucket.Day
                    )
                )
                .AsResult(),
        ["IConsumptionService.ListConsumptionEventsAsync"] = s =>
            Consumption(s)
                .ListConsumptionEventsAsync(new ListConsumptionEventsRequest([Victim]))
                .AsResult(),
        ["IConsumptionService.ListProvidersAsync"] = s =>
            Consumption(s).ListProvidersAsync(Victim).AsResult(),
        ["IConsumptionService.GetEarliestConsumptionAsync"] = s =>
            Consumption(s).GetEarliestConsumptionAsync(Victim).AsResult(),
        ["IConsumptionService.CountAbandonedReservationsAsync"] = s =>
            Consumption(s).CountAbandonedReservationsAsync(Victim).AsResult(),
    };

    public static TheoryData<string> CaseNames() =>
        [
            .. AuditDecoratorCoverageTests
                .ServiceInterfaces()
                .Select(r => r.Data)
                .SelectMany(t => t.GetMethods())
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .Order(StringComparer.Ordinal),
        ];

    [Fact]
    public void ServiceMethods_HaveAnAccountContextCase_ForEveryPublicServiceMethod()
    {
        var missing = CaseNames()
            .Select(r => r.Data)
            .Except(Cases.Keys, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"Add an account context case for: {string.Join(", ", missing)}"
        );
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task ServiceMethod_IsRefused_ForAnotherAccountsIdEvenWithFullPermissions(
        string caseName
    )
    {
        using var scope = BuildServices().CreateScope();

        var result = await Cases[caseName](scope.ServiceProvider);

        var resultCode = result!.GetType().GetProperty("ResultCode")!.GetValue(result)!;
        Assert.Equal("UnauthorizedError", resultCode.ToString());
    }

    private static IGrantService Grant(IServiceProvider s) => s.GetRequiredService<IGrantService>();

    private static IGrantRequestService Request(IServiceProvider s) =>
        s.GetRequiredService<IGrantRequestService>();

    private static IQuotaService Quota(IServiceProvider s) => s.GetRequiredService<IQuotaService>();

    private static IConsumptionService Consumption(IServiceProvider s) =>
        s.GetRequiredService<IConsumptionService>();

    private static ServiceProvider BuildServices()
    {
        var authorization = new Mock<IAuthorizationProvider>();
        authorization.Setup(p => p.HasAccountContext(Attacker)).Returns(true);
        authorization.Setup(p => p.HasAccountContext(Victim)).Returns(false);
        authorization
            .Setup(p =>
                p.IsAuthorizedAsync(It.IsAny<AuthAction>(), It.IsAny<string>(), It.IsAny<Guid[]>())
            )
            .ReturnsAsync(true);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => authorization.Object);
        services.AddScoped(_ => Mock.Of<IUserContextProvider>());
        services.AddScoped<IAuditProvider>(_ => new CapturingAuditProvider());
        services.AddBillingServices(
            BillingOptions
                .Create(new ConfigurationBuilder().Build())
                .RegisterUnit(Page.Value, "page")
                .RegisterOperation(Extraction.Value, "Extraction", GrantKind.Consumable, Page.Value)
                .UseCorelyIamPermissions()
        );
        return services.BuildServiceProvider();
    }
}
