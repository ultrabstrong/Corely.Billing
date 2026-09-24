using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.Components;

public abstract class BillingComponentBase : ComponentBase
{
    [Inject]
    private BillingCallGate Gate { get; set; } = null!;

    protected Task SerializedAsync(Func<Task> work) => Gate.RunAsync(work);
}
