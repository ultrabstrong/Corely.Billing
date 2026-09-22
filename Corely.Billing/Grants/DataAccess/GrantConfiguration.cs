using Corely.Billing;
using Corely.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corely.Billing.Grants.DataAccess;

internal sealed class GrantConfiguration(IDbTypes dbTypes)
    : EntityConfigurationBase<GrantEntity>(dbTypes)
{
    protected override void ConfigureInternal(EntityTypeBuilder<GrantEntity> builder)
    {
        builder.HasKey(e => e.GrantId);
        builder.Property(e => e.GrantId).ValueGeneratedNever();

        builder.Property(e => e.AccountId).IsRequired();
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
        builder.Property(e => e.Quantity).IsRequired();
        builder.Property(e => e.ValidFromUtc).IsRequired();
        builder.Property(e => e.ValidToUtc);

        // Not unique: GrantId is the primary key, so it is already unique globally and a unique
        // index on (AccountId, GrantId) could never reject a row the key does not reject first.
        // The index stays because every read in GrantReader filters on AccountId, and it names
        // only AccountId because GrantId rides along as the clustered-key row locator anyway.
        builder.HasIndex(e => e.AccountId);
    }
}
