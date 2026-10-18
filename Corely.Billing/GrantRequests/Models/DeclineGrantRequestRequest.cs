namespace Corely.Billing.GrantRequests.Models;

public record DeclineGrantRequestRequest(
    Guid AccountId,
    Guid GrantRequestId,
    string? Reason = null,
    Guid? DecidedByUserId = null
);
