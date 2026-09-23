using Corely.Billing.Consumption.Entities;
using Corely.Billing.Grants.Entities;
using Corely.DataAccess.EntityFramework;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.DataAccess;

internal class BillingDbContext : DbContextBase
{
    public BillingDbContext(
        [FromKeyedServices(EFConfigurationKeys.BILLING)] IEFConfiguration efConfiguration
    )
        : base(efConfiguration) { }

    public BillingDbContext(
        DbContextOptions<DbContextBase> opts,
        [FromKeyedServices(EFConfigurationKeys.BILLING)] IEFConfiguration efConfiguration
    )
        : base(opts, efConfiguration) { }

    public DbSet<GrantEntity> Grants => Set<GrantEntity>();
    public DbSet<ConsumptionEventEntity> ConsumptionEvents => Set<ConsumptionEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Type[] configTypes =
        [
            typeof(EntityConfigurationBase<>),
            typeof(EntityConfigurationBase<,>),
        ];
        foreach (var configType in configTypes)
        {
            var configs = GetType()
                .Assembly.GetTypes()
                .Where(t =>
                    t is { IsClass: true, IsAbstract: false, BaseType.IsGenericType: true }
                    && t.BaseType.GetGenericTypeDefinition() == configType
                );

            foreach (var t in configs)
            {
                var cfg = Activator.CreateInstance(t, efConfiguration.GetDbTypes());
                modelBuilder.ApplyConfiguration((dynamic)cfg!);
            }
        }
    }
}
