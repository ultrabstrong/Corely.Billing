using Corely.Billing.CompiledModel.MySql.Generated;

namespace Corely.Billing.CompiledModel.MySql;

public static class BillingOptionsExtensions
{
    extension(BillingOptions options)
    {
        public BillingOptions UseMySqlCompiledModel()
        {
            ArgumentNullException.ThrowIfNull(options);
            var billingVersion = typeof(BillingOptions).Assembly.GetName().Version;
            var modelVersion = typeof(BillingOptionsExtensions).Assembly.GetName().Version;
            if (billingVersion != modelVersion)
            {
                throw new InvalidOperationException(
                    $"Corely.Billing.CompiledModel.MySql {modelVersion} was built for Corely.Billing {modelVersion}, but Corely.Billing {billingVersion} is loaded. Use the same version of both."
                );
            }
            return options.UseCompiledModel(BillingDbContextModel.Instance);
        }
    }
}
