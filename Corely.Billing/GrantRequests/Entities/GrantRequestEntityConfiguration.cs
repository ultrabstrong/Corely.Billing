using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;
using Corely.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corely.Billing.GrantRequests.Entities;

internal sealed class GrantRequestEntityConfiguration(IDbTypes dbTypes)
    : EntityConfigurationBase<GrantRequestEntity>(dbTypes)
{
    protected override void ConfigureInternal(EntityTypeBuilder<GrantRequestEntity> builder)
    {
        builder.HasKey(e => e.GrantRequestId);
        builder.Property(e => e.GrantRequestId).ValueGeneratedNever();

        builder.Property(e => e.AccountId).IsRequired();
        builder
            .Property(e => e.Unit)
            .HasConversion<string?>(
                unit => unit == null ? null : unit.Value.Value,
                value => value == null ? null : UsageUnit.From(value)
            )
            .HasMaxLength(UsageUnit.MAX_LENGTH);
        builder
            .Property(e => e.Kind)
            .HasConversion<string>()
            .HasMaxLength(Grant.KIND_MAX_LENGTH)
            .IsRequired();
        builder
            .Property(e => e.Operation)
            .HasConversion(operation => operation.Value, value => UsageOperation.From(value))
            .HasMaxLength(UsageOperation.MAX_LENGTH)
            .IsRequired();
        builder.Property(e => e.Quantity);
        builder.Property(e => e.Note).HasMaxLength(GrantRequest.NOTE_MAX_LENGTH);
        builder.Property(e => e.RequestedUtc).IsRequired();
        builder
            .Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(GrantRequest.STATUS_MAX_LENGTH)
            .IsRequired();
        builder
            .Property(e => e.DecisionReason)
            .HasMaxLength(GrantRequest.DECISION_REASON_MAX_LENGTH);

        builder.HasIndex(e => new { e.AccountId, e.Status });
    }
}
