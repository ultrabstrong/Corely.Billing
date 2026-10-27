using Corely.Billing.Registrations.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.IAM.Endpoints;

internal sealed record RegisterBody(string? Reference = null, string? Description = null)
{
    public RegisterRequest ToRegisterRequest(Guid accountId, UsageOperation operation) =>
        new(accountId, operation, Reference, Description);
}
