using System.Text.Json;
using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Mappers;
using Corely.Billing.Consumption.Models;

namespace Corely.Billing.UnitTests.Consumption.Mappers;

public class ConsumptionEventMapperTests
{
    private static readonly DateTime Timestamp = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    [Fact]
    public void ToEntity_MapsEveryFieldAndSerializesTags_ForAFullEvent()
    {
        var model = new ConsumptionEvent
        {
            ConsumptionId = Guid.CreateVersion7(),
            AccountId = Guid.CreateVersion7(),
            GrantId = Guid.CreateVersion7(),
            Operation = TestUsage.Other,
            Unit = TestUsage.Page,
            Quantity = 5,
            Provider = "prov",
            UtcTimestamp = Timestamp,
            CorrelationId = Guid.CreateVersion7(),
            IdempotencyKey = "scope|other|page|x",
            FinalizedUtc = Timestamp.AddMinutes(1),
            Outcome = ConsumptionOutcome.Settled,
            UserId = Guid.CreateVersion7(),
            Tags = new() { ["k"] = "v" },
        };

        var entity = model.ToEntity();

        Assert.Equal(model.ConsumptionId, entity.ConsumptionId);
        Assert.Equal(model.AccountId, entity.AccountId);
        Assert.Equal(model.GrantId, entity.GrantId);
        Assert.Equal(model.Operation, entity.Operation);
        Assert.Equal(model.Unit, entity.Unit);
        Assert.Equal(model.Quantity, entity.Quantity);
        Assert.Equal(model.Provider, entity.Provider);
        Assert.Equal(model.UtcTimestamp, entity.UtcTimestamp);
        Assert.Equal(model.CorrelationId, entity.CorrelationId);
        Assert.Equal(model.IdempotencyKey, entity.IdempotencyKey);
        Assert.Equal(model.FinalizedUtc, entity.FinalizedUtc);
        Assert.Equal(model.Outcome, entity.Outcome);
        Assert.Equal(model.UserId, entity.UserId);
        Assert.Equal(
            "v",
            JsonSerializer.Deserialize<Dictionary<string, string>>(entity.TagsJson!)!["k"]
        );
    }

    [Fact]
    public void ToModel_MapsEveryFieldAndDeserializesTags_ForAFullEntity()
    {
        var entity = new ConsumptionEventEntity
        {
            ConsumptionId = Guid.CreateVersion7(),
            AccountId = Guid.CreateVersion7(),
            GrantId = Guid.CreateVersion7(),
            Operation = TestUsage.NoOp,
            Unit = TestUsage.Byte,
            Quantity = 7,
            Provider = "prov2",
            UtcTimestamp = Timestamp,
            CorrelationId = Guid.CreateVersion7(),
            IdempotencyKey = "scope|noop|byte|x",
            FinalizedUtc = null,
            Outcome = null,
            UserId = Guid.CreateVersion7(),
            TagsJson = "{\"a\":\"b\"}",
        };

        var model = entity.ToModel();

        Assert.Equal(entity.ConsumptionId, model.ConsumptionId);
        Assert.Equal(entity.AccountId, model.AccountId);
        Assert.Equal(entity.GrantId, model.GrantId);
        Assert.Equal(entity.Operation, model.Operation);
        Assert.Equal(entity.Unit, model.Unit);
        Assert.Equal(entity.Quantity, model.Quantity);
        Assert.Equal(entity.Provider, model.Provider);
        Assert.Equal(entity.UtcTimestamp, model.UtcTimestamp);
        Assert.Equal(entity.CorrelationId, model.CorrelationId);
        Assert.Equal(entity.IdempotencyKey, model.IdempotencyKey);
        Assert.Null(model.FinalizedUtc);
        Assert.Null(model.Outcome);
        Assert.Equal(entity.UserId, model.UserId);
        Assert.Equal("b", model.Tags!["a"]);
    }

    [Fact]
    public void ToEntity_StoresNoTags_ForAnEventWithoutTags()
    {
        var entity = new ConsumptionEvent { Provider = "prov" }.ToEntity();

        Assert.Null(entity.TagsJson);
    }
}
