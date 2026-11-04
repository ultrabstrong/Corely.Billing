using Corely.Billing.GrantRequests.Enums;

namespace Corely.Billing.Web.Extensions;

internal static class GrantRequestStatusExtensions
{
    extension(GrantRequestStatus status)
    {
        public string HistoryBadgeClass() =>
            status switch
            {
                GrantRequestStatus.Approved => "bg-success-subtle text-success-emphasis",
                GrantRequestStatus.Declined => "bg-danger-subtle text-danger-emphasis",
                GrantRequestStatus.Cancelled => "bg-secondary-subtle text-secondary-emphasis",
                _ => "bg-warning-subtle text-warning-emphasis",
            };
    }
}
