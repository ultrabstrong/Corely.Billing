using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Operations;

namespace Corely.Billing.Consumption.Mappers;

internal static class ConsumptionEventEntityExtensions
{
    extension(ConsumptionEventEntity reservation)
    {
        public ConsumptionEventEntity ToSpillover(
            OperationContext operationContext,
            Guid grantId,
            long quantity,
            DateTime settledUtc
        ) =>
            new()
            {
                ConsumptionId = Guid.CreateVersion7(),
                AccountId = reservation.AccountId,
                Quantity = quantity,
                Unit = reservation.Unit,
                Operation = reservation.Operation,
                Provider = reservation.Provider,
                UtcTimestamp = reservation.UtcTimestamp,
                CorrelationId = reservation.CorrelationId,
                IdempotencyKey = IdempotencyKeyFactory.Create(
                    operationContext,
                    reservation.Operation,
                    reservation.Unit,
                    grantId
                ),
                GrantId = grantId,
                FinalizedUtc = settledUtc,
                Outcome = ConsumptionOutcome.Settled,
                UserId = reservation.UserId,
                TagsJson = reservation.TagsJson,
            };
    }
}
