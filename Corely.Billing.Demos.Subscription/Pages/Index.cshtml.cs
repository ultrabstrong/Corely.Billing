using Corely.Billing.Grants.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Corely.Billing.Demos.Subscription.Pages;

public class IndexModel(Membership membership) : PageModel
{
    public Grant? Subscription { get; private set; }

    public async Task OnGetAsync() => Subscription = await membership.SubscriptionAsync();

    public async Task<IActionResult> OnPostSubscribeAsync()
    {
        await membership.SubscribeAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        await membership.CancelAsync();
        return RedirectToPage();
    }
}
