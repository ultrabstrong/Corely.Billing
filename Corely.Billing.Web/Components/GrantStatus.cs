using Corely.Billing.Grants.Models;

namespace Corely.Billing.Web.Components;

public enum GrantStatus
{
    Upcoming,
    Active,
    Expired,
}

internal static class GrantStatusExtensions
{
    public static GrantStatus StatusAt(this Grant grant, DateTime utcNow) =>
        utcNow < grant.ValidFromUtc ? GrantStatus.Upcoming
        : utcNow > grant.ValidToUtc ? GrantStatus.Expired
        : GrantStatus.Active;
}
