using Corely.Billing.Registrations.Models;

namespace Corely.Billing.Web.Extensions;

internal static class RegistrationStatusExtensions
{
    extension(RegistrationStatus status)
    {
        public string Label() =>
            status switch
            {
                RegistrationStatus.GrantEnded => "Grant ended",
                RegistrationStatus.GrantDisabled => "Grant disabled",
                _ => status.ToString(),
            };

        public string BadgeClass() =>
            status == RegistrationStatus.Active
                ? "bg-success-subtle text-success-emphasis"
                : "bg-warning-subtle text-warning-emphasis";
    }
}
