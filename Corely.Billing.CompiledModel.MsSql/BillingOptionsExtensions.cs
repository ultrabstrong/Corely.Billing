using Corely.Billing.CompiledModel.MsSql.Generated;

namespace Corely.Billing.CompiledModel.MsSql;

public static class BillingOptionsExtensions
{
    extension(BillingOptions options)
    {
        public BillingOptions UseMsSqlCompiledModel()
        {
            ArgumentNullException.ThrowIfNull(options);
            var billingVersion = typeof(BillingOptions).Assembly.GetName().Version;
            var modelVersion = typeof(BillingOptionsExtensions).Assembly.GetName().Version;
            if (billingVersion != modelVersion)
            {
                throw new InvalidOperationException(
                    $"Corely.Billing.CompiledModel.MsSql {modelVersion} was built for Corely.Billing {modelVersion}, but Corely.Billing {billingVersion} is loaded. Use the same version of both."
                );
            }
            return options.UseCompiledModel(BillingDbContextModel.Instance);
        }
    }
}
