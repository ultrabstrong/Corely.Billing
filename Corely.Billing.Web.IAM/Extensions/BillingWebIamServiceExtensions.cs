using Corely.Billing.Web.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corely.Billing.Web.IAM.Extensions;

public static class BillingWebIamServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddBillingWebIam()
        {
            services.AddBillingWeb<IamBillingAccountAccessor>();
            services.Replace(
                ServiceDescriptor.Scoped<IGrantActionGate, PermissionViewGrantActionGate>()
            );
            return services;
        }
    }
}
