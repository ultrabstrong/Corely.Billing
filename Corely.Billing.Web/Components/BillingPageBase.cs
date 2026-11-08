using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Components;

public abstract class BillingPageBase : BillingOperationComponentBase
{
    protected Guid? CurrentAccountId { get; private set; }

    protected bool Loaded { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        CurrentAccountId = await InOperationAsync(services =>
            services.GetRequiredService<IBillingAccountAccessor>().GetAccountIdAsync()
        );
        Loaded = true;
    }
}
