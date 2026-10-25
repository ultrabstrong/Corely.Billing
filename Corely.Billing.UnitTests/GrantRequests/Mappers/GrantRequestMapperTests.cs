using Corely.Billing.GrantRequests.Entities;
using Corely.Billing.GrantRequests.Mappers;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.UnitTests.GrantRequests.Mappers;

public class GrantRequestMapperTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly UsageOperationDefinition Extraction = TestUsage.Vocabulary.Definition(
        TestUsage.Extraction
    )!;

    [Fact]
    public void ToGrantRequest_StartsPendingWithANewId_ForARequest()
    {
        var model = new RequestGrantRequest(AccountId, TestUsage.Extraction, 100).ToGrantRequest(
            Extraction,
            Now
        );

        Assert.Equal(GrantKind.Consumable, model.Kind);
        Assert.Equal(TestUsage.Page, model.Unit);

        Assert.NotEqual(Guid.Empty, model.GrantRequestId);
        Assert.Equal(GrantRequestStatus.Pending, model.Status);
        Assert.Equal(Now, model.RequestedUtc);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(" batch ", "batch")]
    public void ToGrantRequest_TrimsTheNote_ForEachNote(string? note, string? expected) =>
        Assert.Equal(
            expected,
            new RequestGrantRequest(AccountId, TestUsage.Extraction, 1, note)
                .ToGrantRequest(Extraction, Now)
                .Note
        );

    [Fact]
    public void ToModel_RoundTripsEveryField_ForAnEntity()
    {
        var model = new GrantRequest
        {
            GrantRequestId = Guid.CreateVersion7(),
            AccountId = AccountId,
            Operation = TestUsage.Extraction,
            Kind = GrantKind.Consumable,
            Unit = TestUsage.Page,
            Quantity = 100,
            Note = "batch",
            RequestedByUserId = Guid.CreateVersion7(),
            RequestedUtc = Now,
            Status = GrantRequestStatus.Approved,
            DecidedUtc = Now.AddHours(1),
            DecidedByUserId = Guid.CreateVersion7(),
            DecisionReason = "fine",
            GrantId = Guid.CreateVersion7(),
            Tags = new() { ["ref"] = "42" },
        };

        var back = model.ToEntity().ToModel();

        Assert.Equivalent(model, back);
    }

    [Fact]
    public void ToCreateGrantRequest_TakesTheRequestsAmountAndTheApprovalsWindow_ForAnApproval()
    {
        var entity = new GrantRequestEntity
        {
            AccountId = AccountId,
            Operation = TestUsage.Extraction,
            Kind = GrantKind.Consumable,
            Unit = TestUsage.Page,
            Quantity = 100,
            TagsJson = """{"ref":"42"}""",
        };

        var create = entity.ToCreateGrantRequest(
            new ApproveGrantRequestRequest(AccountId, Guid.CreateVersion7(), Now, Now.AddDays(30))
        );

        Assert.Equal(AccountId, create.AccountId);
        Assert.Equal(TestUsage.Extraction, create.Operation);
        Assert.Equal(GrantKind.Consumable, create.Kind);
        Assert.Equal(TestUsage.Page, create.Unit);
        Assert.Equal(100, create.Quantity);
        Assert.Equal(Now, create.ValidFromUtc);
        Assert.Equal(Now.AddDays(30), create.ValidToUtc);
        Assert.Equal("42", create.Tags!["ref"]);
    }
}
