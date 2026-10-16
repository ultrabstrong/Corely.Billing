using Corely.Billing.Grants.Models;
using Corely.Billing.IAM.Auditing;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.IAM.Security.Enums;
using static Corely.Billing.IAM.UnitTests.TestUsage;

namespace Corely.Billing.IAM.UnitTests.Auditing;

public class AuditDecoratorTests
{
    private readonly CapturingAuditProvider _auditProvider = new();

    [Fact]
    public async Task CreateGrant_RecordsACreateInTheAccount_WithTheNewGrant()
    {
        var created = Guid.CreateVersion7();
        var inner = new Mock<IGrantService>();
        inner
            .Setup(s => s.CreateGrantAsync(It.IsAny<CreateGrantRequest>(), default))
            .ReturnsAsync(new CreateGrantResult(CreateGrantResultCode.Success, "", created));

        await new GrantAuditDecorator(inner.Object, _auditProvider).CreateGrantAsync(
            new(AccountId, Extraction, Page, 1, DateTime.UtcNow, DateTime.UtcNow.AddDays(1))
        );

        var captured = Assert.Single(_auditProvider.Calls);
        Assert.Equal(nameof(IGrantService), captured.Call.Service);
        Assert.Equal(nameof(IGrantService.CreateGrantAsync), captured.Operation);
        Assert.Equal(AuthAction.Create, captured.Call.Action);
        Assert.Equal(BillingResourceTypes.GRANT_RESOURCE_TYPE, captured.Call.ResourceType);
        Assert.Equal(AccountId, captured.Call.AccountId);
        Assert.Equal([created], captured.Outcome!.ResourceIds);
    }

    [Fact]
    public async Task DeleteGrant_RecordsADeleteOfThatGrant()
    {
        var inner = new Mock<IGrantService>();
        inner
            .Setup(s => s.DeleteGrantAsync(AccountId, GrantId, default))
            .ReturnsAsync(new DeleteGrantResult(DeleteGrantResultCode.Success, ""));

        await new GrantAuditDecorator(inner.Object, _auditProvider).DeleteGrantAsync(
            AccountId,
            GrantId
        );

        var captured = Assert.Single(_auditProvider.Calls);
        Assert.Equal(AuthAction.Delete, captured.Call.Action);
        Assert.Equal([GrantId], captured.Call.ResourceIds);
    }

    [Fact]
    public async Task ListProviders_RecordsAReadOfConsumption()
    {
        var inner = new Mock<IConsumptionService>();

        await new ConsumptionAuditDecorator(inner.Object, _auditProvider).ListProvidersAsync(
            AccountId
        );

        var captured = Assert.Single(_auditProvider.Calls);
        Assert.Equal(nameof(IConsumptionService), captured.Call.Service);
        Assert.Equal(AuthAction.Read, captured.Call.Action);
        Assert.Equal(BillingResourceTypes.CONSUMPTION_RESOURCE_TYPE, captured.Call.ResourceType);
        Assert.Equal(AccountId, captured.Call.AccountId);
    }

    [Fact]
    public async Task Reserve_RecordsAnExecuteOfQuota()
    {
        var inner = new Mock<IQuotaService>();
        inner
            .Setup(s => s.ReserveAsync(It.IsAny<ReserveQuotaRequest>(), default))
            .ReturnsAsync(new ReserveQuotaResult(ReserveQuotaResultCode.Success, ""));

        await new QuotaAuditDecorator(inner.Object, _auditProvider).ReserveAsync(
            new(AccountId, Extraction, Page, 1, "provider")
        );

        var captured = Assert.Single(_auditProvider.Calls);
        Assert.Equal(AuthAction.Execute, captured.Call.Action);
        Assert.Equal(BillingResourceTypes.QUOTA_RESOURCE_TYPE, captured.Call.ResourceType);
        Assert.Equal(nameof(ReserveQuotaResultCode.Success), captured.Outcome!.ResultCode);
    }

    [Fact]
    public async Task GetAvailability_RecordsTheAvailabilityAsTheResult()
    {
        var inner = new Mock<IQuotaService>();
        inner
            .Setup(s => s.GetAvailabilityAsync(AccountId, Extraction, Page, default))
            .ReturnsAsync(QuotaAvailability.Exhausted);

        await new QuotaAuditDecorator(inner.Object, _auditProvider).GetAvailabilityAsync(
            AccountId,
            Extraction,
            Page
        );

        Assert.Equal(
            nameof(QuotaAvailability.Exhausted),
            Assert.Single(_auditProvider.Calls).Outcome!.ResultCode
        );
    }
}
