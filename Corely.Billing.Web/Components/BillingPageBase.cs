using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Components;

public abstract class BillingPageBase : OwningComponentBase
{
    protected Guid? CurrentAccountId { get; private set; }

    protected bool Loaded { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        CurrentAccountId = await ScopedServices
            .GetRequiredService<IBillingAccountAccessor>()
            .GetAccountIdAsync();
        Loaded = true;
    }
}
