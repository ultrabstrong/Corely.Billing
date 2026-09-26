using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.UnitTests;

public sealed class RecordingGrantActionGate : IGrantActionGate
{
    private readonly HashSet<GrantAction> _denied = [];

    public List<(GrantAction Action, Guid? GrantId)> Calls { get; } = [];

    public void Deny(params GrantAction[] actions) => _denied.UnionWith(actions);

    public RenderFragment Gate(
        GrantAction action,
        Guid? grantId,
        RenderFragment authorized,
        RenderFragment? notAuthorized = null
    )
    {
        Calls.Add((action, grantId));
        return _denied.Contains(action) ? notAuthorized ?? (_ => { }) : authorized;
    }
}
