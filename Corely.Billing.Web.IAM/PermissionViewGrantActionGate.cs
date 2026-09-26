using Corely.Billing.IAM;
using Corely.Billing.Web.IAM.Extensions;
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
    ) =>
        builder =>
        {
            builder.OpenComponent<PermissionView>(0);
            builder.AddComponentParameter(1, nameof(PermissionView.Action), action.ToAuthAction());
            builder.AddComponentParameter(
                2,
                nameof(PermissionView.Resource),
                BillingResourceTypes.GRANT_RESOURCE_TYPE
            );
            builder.AddComponentParameter(
                3,
                nameof(PermissionView.ResourceIds),
                grantId is { } id ? new[] { id } : null
            );
            builder.AddComponentParameter(4, nameof(PermissionView.Authorized), authorized);
            builder.AddComponentParameter(5, nameof(PermissionView.NotAuthorized), notAuthorized);
            builder.CloseComponent();
        };
}
