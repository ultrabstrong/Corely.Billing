using Corely.Billing.Consumption.Entities;
using Corely.Billing.Consumption.Mappers;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Operations;

namespace Corely.Billing.UnitTests.Consumption.Mappers;

public class ConsumptionEventEntityExtensionsTests
{
    private static readonly OperationContext Operation = new(Guid.CreateVersion7(), "job:1/step:1");
    private static readonly Guid SpillGrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime SettledUtc = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private static ConsumptionEventEntity Reservation() =>
        new()
        {
            ConsumptionId = Guid.CreateVersion7(),
            AccountId = Guid.CreateVersion7(),
            GrantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = 1,
            Provider = "prov",
            UtcTimestamp = SettledUtc.AddMinutes(-5),
            CorrelationId = Guid.CreateVersion7(),
            IdempotencyKey = "reservation-key",
            UserId = Guid.CreateVersion7(),
            TagsJson = "{\"plan\":\"growth\"}",
        };

    [Fact]
    public void ToSpillover_IsASettledRowOnTheNewGrant_ForAReservation()
    {
        var reservation = Reservation();

        var spill = reservation.ToSpillover(Operation, SpillGrantId, 37, SettledUtc);

        Assert.Equal(SpillGrantId, spill.GrantId);
        Assert.Equal(37, spill.Quantity);
        Assert.Equal(ConsumptionOutcome.Settled, spill.Outcome);
        Assert.Equal(SettledUtc, spill.FinalizedUtc);
        Assert.NotEqual(reservation.ConsumptionId, spill.ConsumptionId);
        Assert.Equal(
            IdempotencyKeyFactory.Create(
                Operation,
                TestUsage.Extraction,
                TestUsage.Page,
                SpillGrantId
            ),
            spill.IdempotencyKey
        );
    }

    [Fact]
    public void ToSpillover_KeepsWhoAndWhatAndWhen_ForAReservation()
    {
        var reservation = Reservation();

        var spill = reservation.ToSpillover(Operation, SpillGrantId, 37, SettledUtc);

        Assert.Equal(
            (
                reservation.AccountId,
                reservation.Operation,
                reservation.Unit,
                reservation.Provider,
                reservation.UtcTimestamp,
                reservation.CorrelationId,
                reservation.UserId,
                reservation.TagsJson
            ),
            (
                spill.AccountId,
                spill.Operation,
                spill.Unit,
                spill.Provider,
                spill.UtcTimestamp,
                spill.CorrelationId,
                spill.UserId,
                spill.TagsJson
            )
        );
    }
}
