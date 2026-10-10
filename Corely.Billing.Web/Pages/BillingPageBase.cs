using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Pages;

public abstract class BillingPageBase : OwningComponentBase
{
    protected Guid? AccountId { get; private set; }

    protected bool Loaded { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        AccountId = await ScopedServices
            .GetRequiredService<IBillingAccountAccessor>()
            .GetAccountIdAsync();
        Loaded = true;
    }
}
