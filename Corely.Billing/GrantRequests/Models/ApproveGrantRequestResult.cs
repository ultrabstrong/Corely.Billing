namespace Corely.Billing.GrantRequests.Models;

public record ApproveGrantRequestResult(
    DecideGrantRequestResultCode ResultCode,
    string Message,
    Guid GrantId
);
