namespace Corely.Billing.GrantRequests.Models;

public record CancelGrantRequestRequest(
    Guid AccountId,
    Guid GrantRequestId,
    string? Reason = null,
    Guid? CancelledByUserId = null
);
