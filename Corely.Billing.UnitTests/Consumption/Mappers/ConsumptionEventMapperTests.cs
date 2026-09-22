using System.Text.Json;
using Corely.Billing;
using Corely.Billing.Consumption.DataAccess;
using Corely.Billing.Consumption.Mappers;
using Corely.Billing.Consumption.Models;

namespace Corely.Billing.UnitTests.Consumption.Mappers;

public class ConsumptionEventMapperTests
{
    private static readonly Guid TestConsumptionId1 = Guid.Parse(
        "11111111-1111-1111-1111-111111111111"
    );
    private static readonly Guid TestConsumptionId2 = Guid.Parse(
        "22222222-2222-2222-2222-222222222222"
    );
    private static readonly Guid TestGrantId1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TestGrantId2 = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid TestAccountId1 = Guid.Parse(
        "55555555-5555-5555-5555-555555555555"
    );
    private static readonly Guid TestAccountId2 = Guid.Parse(
        "66666666-6666-6666-6666-666666666666"
    );
    private static readonly Guid TestUserId1 = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid TestUserId2 = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public void ToEntity_Maps_All_Fields_And_Serializes_Tags()
    {
        // Built through FromEntity rather than Create because this is the rehydration path the
        // read direction uses; Create would work equally well now that it accepts an id.
        var model = ConsumptionEvent.FromEntity(
            new ConsumptionEventEntity
            {
                ConsumptionId = TestConsumptionId1,
                AccountId = TestAccountId1,
                Quantity = 5,
                Unit = TestUsage.Page,
                Operation = TestUsage.Other,
                Provider = "prov",
                UtcTimestamp = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                CorrelationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                GrantId = TestGrantId1,
                UserId = TestUserId1,
                TagsJson = "{\"k\":\"v\"}",
            }
        );

        var entity = model.ToEntity();

        Assert.Equal(TestConsumptionId1, entity.ConsumptionId);
        Assert.Equal(TestAccountId1, entity.AccountId);
        Assert.Equal(5, entity.Quantity);
        Assert.Equal(TestUsage.Page, entity.Unit);
        Assert.Equal(TestUsage.Other, entity.Operation);
        Assert.Equal("prov", entity.Provider);
        Assert.Equal(new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc), entity.UtcTimestamp);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), entity.CorrelationId);
        Assert.Equal(TestGrantId1, entity.GrantId);
        Assert.Equal(TestUserId1, entity.UserId);
        Assert.NotNull(entity.TagsJson);

        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.TagsJson!);
        Assert.NotNull(dict);
        Assert.Equal("v", dict!["k"]);
    }

    [Fact]
    public void ToModel_Maps_All_Fields_And_Deserializes_Tags()
    {
        var entity = new ConsumptionEventEntity
        {
            ConsumptionId = TestConsumptionId2,
            AccountId = TestAccountId2,
            Quantity = 7,
            Unit = TestUsage.Byte,
            Operation = TestUsage.Unknown,
            Provider = "prov2",
            UtcTimestamp = new DateTime(2024, 12, 31, 1, 2, 3, DateTimeKind.Utc),
            CorrelationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            GrantId = TestGrantId2,
            UserId = TestUserId2,
            TagsJson = "{\"a\":\"b\"}",
        };

        var model = ConsumptionEvent.FromEntity(entity);

        Assert.Equal(TestConsumptionId2, model.ConsumptionId);
        Assert.Equal(TestAccountId2, model.AccountId);
        Assert.Equal(7, model.Quantity);
        Assert.Equal(TestUsage.Byte, model.Unit);
        Assert.Equal(TestUsage.Unknown, model.Operation);
        Assert.Equal("prov2", model.Provider);
        Assert.Equal(new DateTime(2024, 12, 31, 1, 2, 3, DateTimeKind.Utc), model.UtcTimestamp);
        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), model.CorrelationId);
        Assert.Equal(TestUserId2, model.UserId);
        Assert.Equal(TestGrantId2, model.GrantId);
        Assert.NotNull(model.Tags);
        Assert.Equal("b", model.Tags!["a"]);
    }
}
