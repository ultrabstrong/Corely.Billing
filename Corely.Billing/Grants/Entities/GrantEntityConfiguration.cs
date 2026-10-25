using Corely.Billing.Grants.Constants;
using Corely.Billing.Usage;
using Corely.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corely.Billing.Grants.Entities;

internal sealed class GrantEntityConfiguration(IDbTypes dbTypes)
    : EntityConfigurationBase<GrantEntity>(dbTypes)
{
    protected override void ConfigureInternal(EntityTypeBuilder<GrantEntity> builder)
    {
        builder.HasKey(e => e.GrantId);
        builder.Property(e => e.GrantId).ValueGeneratedNever();

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
            .HasMaxLength(GrantConstants.KIND_MAX_LENGTH)
            .IsRequired();
        builder
            .Property(e => e.Operation)
            .HasConversion(operation => operation.Value, value => UsageOperation.From(value))
            .HasMaxLength(UsageOperation.MAX_LENGTH)
            .IsRequired();
        builder.Property(e => e.Quantity);
        builder.Property(e => e.ValidFromUtc).IsRequired();
        builder.Property(e => e.ValidToUtc);
        builder.Property(e => e.Disabled).IsRequired();

        builder.HasIndex(e => new { e.AccountId, e.Operation });
    }
}
