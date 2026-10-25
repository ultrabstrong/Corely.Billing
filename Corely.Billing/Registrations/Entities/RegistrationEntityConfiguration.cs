using Corely.Billing.Registrations.Constants;
using Corely.Billing.Usage;
using Corely.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corely.Billing.Registrations.Entities;

internal sealed class RegistrationEntityConfiguration(IDbTypes dbTypes)
    : EntityConfigurationBase<RegistrationEntity>(dbTypes)
{
    protected override void ConfigureInternal(EntityTypeBuilder<RegistrationEntity> builder)
    {
        builder.HasKey(e => e.RegistrationId);
        builder.Property(e => e.RegistrationId).ValueGeneratedNever();

        builder.Property(e => e.AccountId).IsRequired();
        builder.Property(e => e.GrantId).IsRequired();
        builder
            .Property(e => e.Operation)
            .HasConversion(operation => operation.Value, value => UsageOperation.From(value))
            .HasMaxLength(UsageOperation.MAX_LENGTH)
            .IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(RegistrationConstants.REFERENCE_MAX_LENGTH);
        builder
            .Property(e => e.Description)
            .HasMaxLength(RegistrationConstants.DESCRIPTION_MAX_LENGTH);
        builder.Property(e => e.RegisteredUtc).IsRequired();
        builder.Property(e => e.ReleasedUtc);

        builder.HasIndex(e => new { e.GrantId, e.ReleasedUtc });
        builder.HasIndex(e => new
        {
            e.AccountId,
            e.Operation,
            e.Reference,
        });
    }
}
