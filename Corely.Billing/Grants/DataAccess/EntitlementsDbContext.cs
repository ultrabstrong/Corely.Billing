using Corely.DataAccess.EntityFramework;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Corely.Billing.Grants.DataAccess;

public class EntitlementsDbContext : DbContextBase
{
    public EntitlementsDbContext(IEFConfiguration efConfiguration)
        : base(efConfiguration) { }

    public EntitlementsDbContext(
        DbContextOptions<DbContextBase> opts,
        IEFConfiguration efConfiguration
    )
        : base(opts, efConfiguration) { }

    public DbSet<GrantEntity> Grants => Set<GrantEntity>();

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
