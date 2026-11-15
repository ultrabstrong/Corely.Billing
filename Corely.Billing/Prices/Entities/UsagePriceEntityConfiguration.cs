using Corely.Billing.Prices.Models;
using Corely.Billing.Usage;
using Corely.DataAccess;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corely.Billing.Prices.Entities;

internal sealed class UsagePriceEntityConfiguration(IDbTypes dbTypes)
    : EntityConfigurationBase<UsagePriceEntity>(dbTypes)
{
    protected override void ConfigureInternal(EntityTypeBuilder<UsagePriceEntity> builder)
    {
        builder.HasKey(e => e.Operation);
        builder
            .Property(e => e.Operation)
            .HasConversion(operation => operation.Value, value => UsageOperation.From(value))
            .HasMaxLength(UsageOperation.MAX_LENGTH)
            .ValueGeneratedNever();
        builder
            .Property(e => e.Amount)
            .HasPrecision(Money.AMOUNT_PRECISION, Money.AMOUNT_DECIMALS)
            .IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(Money.CURRENCY_LENGTH).IsRequired();
    }
}
