using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web;

internal sealed class OpenGrantActionGate : IGrantActionGate
{
    public RenderFragment Gate(
        GrantAction action,
        Guid? grantId,
        RenderFragment authorized,
        RenderFragment? notAuthorized = null
    ) => authorized;
}
