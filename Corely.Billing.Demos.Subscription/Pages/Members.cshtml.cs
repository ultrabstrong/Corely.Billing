using Corely.Billing.Registrations.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Corely.Billing.Demos.Subscription.Pages;

public class MembersModel(Membership membership) : PageModel
{
    public string CapacityText { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if ((await membership.CapacityAsync()).Grants.Count == 0)
            return RedirectToPage("/Index");

        var joined = await membership.JoinAsync(Membership.YOU);
        if (
            joined.ResultCode
            is not (RegisterResultCode.Registered or RegisterResultCode.AlreadyRegisteredError)
        )
        {
            return RedirectToPage("/Index");
        }

        var capacity = await membership.CapacityAsync();
        CapacityText = $"{capacity.InUse:N0} of {capacity.Granted:N0} members in use";
        return Page();
    }
}
