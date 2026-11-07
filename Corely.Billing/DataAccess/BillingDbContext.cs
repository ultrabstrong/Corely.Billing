using Corely.Billing.Consumption.Entities;
using Corely.Billing.GrantRequests.Entities;
using Corely.Billing.Grants.Entities;
using Corely.Billing.Registrations.Entities;
using Corely.DataAccess.EntityFramework;
using Corely.DataAccess.EntityFramework.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.DataAccess;

internal class BillingDbContext : DbContextBase
{
    public const string DEFAULT_MIGRATIONS_HISTORY_TABLE = "__CorelyBillingMigrationsHistory";

    public BillingDbContext(
        [FromKeyedServices(EFConfigurationKeys.BILLING)] IEFConfiguration efConfiguration
    )
        : base(efConfiguration) { }

    public BillingDbContext(
        DbContextOptions<DbContextBase> opts,
        [FromKeyedServices(EFConfigurationKeys.BILLING)] IEFConfiguration efConfiguration,
        [FromKeyedServices(EFConfigurationKeys.BILLING)] BillingCompiledModel compiledModel
    )
        : base(opts, efConfiguration)
    {
        _compiledModel = compiledModel.Model;
    }

    private readonly IModel? _compiledModel;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (_compiledModel is not null)
        {
            optionsBuilder.UseModel(_compiledModel);
        }
    }

    public DbSet<GrantEntity> Grants => Set<GrantEntity>();
    public DbSet<GrantRequestEntity> GrantRequests => Set<GrantRequestEntity>();
    public DbSet<ConsumptionEventEntity> ConsumptionEvents => Set<ConsumptionEventEntity>();
    public DbSet<RegistrationEntity> Registrations => Set<RegistrationEntity>();

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
