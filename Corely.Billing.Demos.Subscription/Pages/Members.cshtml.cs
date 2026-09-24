using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Corely.Billing.Demos.Subscription.Pages;

public class MembersModel(Membership membership) : PageModel
{
    public string VisitsText { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await membership.IsEntitledAsync())
            return RedirectToPage("/Index");

        await membership.RecordVisitAsync();
        var term = await membership.CurrentTermAsync();
        var visits = term is null ? 0 : await membership.VisitsThisTermAsync(term);
        VisitsText = visits == 1 ? "once" : $"{visits:N0} times";
        return Page();
    }
}
