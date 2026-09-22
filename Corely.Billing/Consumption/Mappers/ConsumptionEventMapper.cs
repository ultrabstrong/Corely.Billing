using Corely.Billing;
using Corely.Billing.Consumption.DataAccess;
using Corely.Billing.Consumption.Models;

namespace Corely.Billing.Consumption.Mappers;

internal static class ConsumptionEventMapper
{
    public static ConsumptionEventEntity ToEntity(this ConsumptionEvent model) =>
        new()
        {
            ConsumptionId = model.ConsumptionId,
            AccountId = model.AccountId,
            Quantity = model.Quantity,
            Unit = model.Unit,
            Operation = model.Operation,
            Provider = model.Provider,
            UtcTimestamp = model.UtcTimestamp,
            CorrelationId = model.CorrelationId,
            IdempotencyKey = model.IdempotencyKey,
            GrantId = model.GrantId,
            FinalizedUtc = model.FinalizedUtc,
            Outcome = model.Outcome,
            UserId = model.UserId,
            TagsJson = TagSerializer.Serialize(model.Tags),
        };
}
