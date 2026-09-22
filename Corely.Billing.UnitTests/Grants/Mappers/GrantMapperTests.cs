using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Mappers;
using Corely.Billing.Grants.Models;

namespace Corely.Billing.UnitTests.Grants.Mappers;

public class GrantMapperTests
{
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToGrant_AssignsANewId_ForACreateRequest()
    {
        var request = new CreateGrantRequest(
            Guid.CreateVersion7(),
            TestUsage.Extraction,
            TestUsage.Page,
            100,
            From,
            To,
            new() { ["plan"] = "standard" }
        );

        var grant = request.ToGrant();

        Assert.NotEqual(Guid.Empty, grant.GrantId);
        Assert.Equal(request.AccountId, grant.AccountId);
        Assert.Equal(request.Operation, grant.Operation);
        Assert.Equal(request.Unit, grant.Unit);
        Assert.Equal(request.Quantity, grant.Quantity);
        Assert.Equal(request.ValidFromUtc, grant.ValidFromUtc);
        Assert.Equal(request.ValidToUtc, grant.ValidToUtc);
        Assert.Equal("standard", grant.Tags!["plan"]);
    }

    [Fact]
    public void ToModel_RoundTripsEveryField_ForAnEntityFromToEntity()
    {
        var grant = new Grant
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = Guid.CreateVersion7(),
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = 100,
            ValidFromUtc = From,
            ValidToUtc = To,
            Tags = new() { ["plan"] = "standard" },
        };

        var roundTripped = grant.ToEntity().ToModel();

        Assert.Equal(grant.GrantId, roundTripped.GrantId);
        Assert.Equal(grant.AccountId, roundTripped.AccountId);
        Assert.Equal(grant.Operation, roundTripped.Operation);
        Assert.Equal(grant.Unit, roundTripped.Unit);
        Assert.Equal(grant.Quantity, roundTripped.Quantity);
        Assert.Equal(grant.ValidFromUtc, roundTripped.ValidFromUtc);
        Assert.Equal(grant.ValidToUtc, roundTripped.ValidToUtc);
        Assert.Equal(grant.Tags, roundTripped.Tags);
    }

    [Fact]
    public void ToEntity_StoresNoTags_ForAGrantWithoutTags()
    {
        var entity = new Grant { GrantId = Guid.CreateVersion7() }.ToEntity();

        Assert.Null(entity.TagsJson);
    }

    [Fact]
    public void ApplyTo_KeepsTheEntitysOperation_ForAnUpdateRequest()
    {
        var entity = new GrantEntity
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = Guid.CreateVersion7(),
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
        };
        var request = new UpdateGrantRequest(
            entity.AccountId,
            entity.GrantId,
            TestUsage.Document,
            50,
            From,
            To
        );

        var grant = request.ApplyTo(entity);

        Assert.Equal(TestUsage.Extraction, grant.Operation);
        Assert.Equal(TestUsage.Document, grant.Unit);
        Assert.Equal(50, grant.Quantity);
    }
}
