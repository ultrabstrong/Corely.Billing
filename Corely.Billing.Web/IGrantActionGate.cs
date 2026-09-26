using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web;

public interface IGrantActionGate
{
    RenderFragment Gate(
        GrantAction action,
        Guid? grantId,
        RenderFragment authorized,
        RenderFragment? notAuthorized = null
    );
}
