using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.Pages;

public abstract class BillingPageBase : ComponentBase
{
    [Inject]
    private IBillingAccountAccessor AccountAccessor { get; set; } = null!;

    protected Guid? AccountId { get; private set; }

    protected bool CanManage { get; private set; }

    protected bool Loaded { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        AccountId = await AccountAccessor.GetAccountIdAsync();
        CanManage = AccountId is not null && await AccountAccessor.CanManageGrantsAsync();
        Loaded = true;
    }
}
