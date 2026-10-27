using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;

namespace Corely.Billing.Web.Extensions;

internal static class GrantKindExtensions
{
    extension(GrantKind kind)
    {
        public string GroupLabel() =>
            kind switch
            {
                GrantKind.Consumable => "Balance",
                GrantKind.Capacity => "Capacity",
                GrantKind.Allowance => "Allowance",
                _ => "Feature",
            };
    }
}
