using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Serialization;

namespace Corely.Billing.Consumption.Mappers;

internal static class ConsumptionEventMapper
{
    extension(ConsumptionEvent model)
    {
        public ConsumptionEventEntity ToEntity() =>
            new()
            {
                ConsumptionId = model.ConsumptionId,
                AccountId = model.AccountId,
                GrantId = model.GrantId,
                Operation = model.Operation,
                Unit = model.Unit,
                Quantity = model.Quantity,
                Provider = model.Provider,
                UtcTimestamp = model.UtcTimestamp,
                CorrelationId = model.CorrelationId,
                IdempotencyKey = model.IdempotencyKey,
                FinalizedUtc = model.FinalizedUtc,
                Outcome = model.Outcome,
                UserId = model.UserId,
                TagsJson = TagSerializer.Serialize(model.Tags),
            };
    }

    extension(ConsumptionEventEntity entity)
    {
        public ConsumptionEvent ToModel() =>
            new()
            {
                ConsumptionId = entity.ConsumptionId,
                AccountId = entity.AccountId,
                GrantId = entity.GrantId,
                Operation = entity.Operation,
                Unit = entity.Unit,
                Quantity = entity.Quantity,
                Provider = entity.Provider,
                UtcTimestamp = entity.UtcTimestamp,
                CorrelationId = entity.CorrelationId,
                IdempotencyKey = entity.IdempotencyKey,
                FinalizedUtc = entity.FinalizedUtc,
                Outcome = entity.Outcome,
                UserId = entity.UserId,
                Tags = TagSerializer.Deserialize(entity.TagsJson),
            };
    }
}
