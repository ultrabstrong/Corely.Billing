using Corely.Billing.IAM.Authorization;
using Corely.Billing.Services;
using Corely.IAM.Security.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IAM.Extensions;

public static class BillingOptionsExtensions
{
    extension(BillingOptions options)
    {
        public BillingOptions UseCorelyIamPermissions() =>
            options.DecorateServices(services =>
            {
                if (!services.Any(d => d.ServiceType == typeof(IAuthorizationProvider)))
                {
                    throw new InvalidOperationException(
                        "UseCorelyIamPermissions needs Corely.IAM. Call AddIAMServices before "
                            + "AddBillingServices."
                    );
                }

                services.Decorate<IGrantService, GrantAuthorizationDecorator>();
                services.Decorate<IConsumptionService, ConsumptionAuthorizationDecorator>();
                services.Decorate<IQuotaService, QuotaAuthorizationDecorator>();
            });
    }
}
