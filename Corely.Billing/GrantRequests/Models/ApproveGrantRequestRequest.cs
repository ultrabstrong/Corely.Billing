namespace Corely.Billing.GrantRequests.Models;

public record ApproveGrantRequestRequest(
    Guid AccountId,
    Guid GrantRequestId,
    DateTime ValidFromUtc,
    DateTime ValidToUtc,
    Guid? DecidedByUserId = null
);
