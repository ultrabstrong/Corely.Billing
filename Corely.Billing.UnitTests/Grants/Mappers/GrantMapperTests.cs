using System.Text.Json;
using Corely.Billing;
using Corely.Billing.Grants.DataAccess;
using Corely.Billing.Grants.Mappers;
using Corely.Billing.Grants.Models;

namespace Corely.Billing.UnitTests.Grants.Mappers;

public class GrantMapperTests
{
    private static readonly Guid TestGrantId1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TestGrantId2 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid TestAccountId1 = Guid.Parse(
        "11111111-1111-1111-1111-111111111111"
    );
    private static readonly Guid TestAccountId2 = Guid.Parse(
        "22222222-2222-2222-2222-222222222222"
    );

    [Fact]
    public void ToEntity_Maps_All_Fields_And_Serializes_Tags()
    {
        var createResult = Grant.Create(
            accountId: TestAccountId1,
            quantity: 5,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            validFromUtc: new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            validToUtc: new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            grantId: TestGrantId1,
            tags: new Dictionary<string, string> { ["k"] = "v" }
        );
        Assert.True(createResult.IsSuccess);

        var entity = createResult.Value.ToEntity();

        Assert.Equal(TestGrantId1, entity.GrantId);
        Assert.Equal(TestAccountId1, entity.AccountId);
        Assert.Equal(5, entity.Quantity);
        Assert.Equal(TestUsage.Page, entity.Unit);
        Assert.Equal(TestUsage.Extraction, entity.Operation);
        Assert.Equal(new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc), entity.ValidFromUtc);
        Assert.Equal(new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc), entity.ValidToUtc);
        Assert.NotNull(entity.TagsJson);

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.TagsJson!);
        Assert.NotNull(dict);
        Assert.Equal("v", dict!["k"]);
    }

    [Fact]
    public void ToModel_Maps_All_Fields_And_Deserializes_Tags()
    {
        var entity = new GrantEntity
        {
            GrantId = TestGrantId2,
            AccountId = TestAccountId2,
            Quantity = 7,
            Unit = TestUsage.Byte,
            Operation = TestUsage.Unknown,
            ValidFromUtc = new DateTime(2024, 12, 31, 1, 2, 3, DateTimeKind.Utc),
            ValidToUtc = new DateTime(2025, 1, 1, 2, 3, 4, DateTimeKind.Utc),
            TagsJson = "{\"a\":\"b\"}",
        };

        var model = Grant.FromEntity(entity);

        Assert.Equal(TestGrantId2, model.GrantId);
        Assert.Equal(TestAccountId2, model.AccountId);
        Assert.Equal(7, model.Quantity);
        Assert.Equal(TestUsage.Byte, model.Unit);
        Assert.Equal(TestUsage.Unknown, model.Operation);
        Assert.Equal(new DateTime(2024, 12, 31, 1, 2, 3, DateTimeKind.Utc), model.ValidFromUtc);
        Assert.Equal(new DateTime(2025, 1, 1, 2, 3, 4, DateTimeKind.Utc), model.ValidToUtc);
        Assert.NotNull(model.Tags);
        Assert.Equal("b", model.Tags!["a"]);
    }
}
