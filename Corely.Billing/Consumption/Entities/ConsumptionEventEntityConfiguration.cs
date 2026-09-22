using Corely.Billing.Consumption.Constants;
using Corely.Billing.Consumption.Models;
using Corely.Billing.Usage;
using Corely.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corely.Billing.Consumption.Entities;

internal sealed class ConsumptionEventEntityConfiguration(IDbTypes dbTypes)
    : EntityConfigurationBase<ConsumptionEventEntity>(dbTypes)
{
    protected override void ConfigureInternal(EntityTypeBuilder<ConsumptionEventEntity> builder)
    {
        builder.HasKey(e => e.ConsumptionId);
        builder.Property(e => e.ConsumptionId).ValueGeneratedNever();

        builder.Property(e => e.AccountId).IsRequired();
        builder.Property(e => e.CorrelationId).IsRequired();
        builder.Property(e => e.GrantId).IsRequired();

        builder
            .Property(e => e.Unit)
            .HasConversion(unit => unit.Value, value => UsageUnit.From(value))
            .HasMaxLength(UsageUnit.MAX_LENGTH)
            .IsRequired();
        builder
            .Property(e => e.Operation)
            .HasConversion(operation => operation.Value, value => UsageOperation.From(value))
            .HasMaxLength(UsageOperation.MAX_LENGTH)
            .IsRequired();
        // Stored as its name rather than an ordinal, so the column reads during a dispute, and
        // length-capped so it does not become nvarchar(max) for an eight-character word.
        builder
            .Property(e => e.Outcome)
            .HasConversion<string>()
            .HasMaxLength(ConsumptionConstants.OUTCOME_MAX_LENGTH);

        builder
            .Property(e => e.Provider)
            .HasMaxLength(ConsumptionConstants.PROVIDER_MAX_LENGTH)
            .IsRequired();

        builder
            .Property(e => e.IdempotencyKey)
            .HasMaxLength(ConsumptionConstants.IDEMPOTENCY_KEY_MAX_LENGTH)
            .IsRequired();

        // The whole double-charge defence is this line. A retry rebuilds the same key and the
        // second insert is rejected by the database rather than by anything the application
        // remembered to check.
        builder.HasIndex(e => new { e.AccountId, e.IdempotencyKey }).IsUnique();

        // Correlation is no longer unique: one unit of work spanning two grants writes two rows
        // under one correlation id, and a reconciliation delta later adds a third.
        builder.HasIndex(e => new { e.AccountId, e.CorrelationId });
    }
}
