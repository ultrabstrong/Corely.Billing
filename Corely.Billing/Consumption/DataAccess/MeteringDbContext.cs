using Corely.DataAccess.EntityFramework;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Corely.Billing.Consumption.DataAccess;

public class MeteringDbContext : DbContextBase
{
    public MeteringDbContext(IEFConfiguration efConfiguration)
        : base(efConfiguration) { }

    public MeteringDbContext(DbContextOptions<DbContextBase> opts, IEFConfiguration efConfiguration)
        : base(opts, efConfiguration) { }

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
                    t.Namespace == GetType().Namespace
                    && t is { IsClass: true, IsAbstract: false, BaseType.IsGenericType: true }
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
