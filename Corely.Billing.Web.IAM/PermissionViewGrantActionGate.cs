using Corely.Billing.Web.IAM.Extensions;
using Corely.IAM.Security.Enums;
using Corely.IAM.Web.Components.Shared;
using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.IAM;

internal sealed class PermissionViewGrantActionGate : IGrantActionGate
{
    public RenderFragment Gate(
        GrantAction action,
        Guid? grantId,
        RenderFragment authorized,
        RenderFragment? notAuthorized = null
    )
    {
        var permissions = action.Permissions();
        var fragment = authorized;
        for (var i = permissions.Count - 1; i >= 0; i--)
        {
            fragment = View(
                permissions[i].Action,
                permissions[i].Resource,
                i == 0 ? grantId : null,
                fragment,
                notAuthorized
            );
        }
        return fragment;
    }

    private static RenderFragment View(
        AuthAction action,
        string resource,
        Guid? resourceId,
        RenderFragment authorized,
        RenderFragment? notAuthorized
    ) =>
        builder =>
        {
            builder.OpenComponent<PermissionView>(0);
            builder.AddComponentParameter(1, nameof(PermissionView.Action), action);
            builder.AddComponentParameter(2, nameof(PermissionView.Resource), resource);
            builder.AddComponentParameter(
                3,
                nameof(PermissionView.ResourceIds),
                resourceId is { } id ? new[] { id } : null
            );
            builder.AddComponentParameter(4, nameof(PermissionView.Authorized), authorized);
            builder.AddComponentParameter(5, nameof(PermissionView.NotAuthorized), notAuthorized);
            builder.CloseComponent();
        };
}
