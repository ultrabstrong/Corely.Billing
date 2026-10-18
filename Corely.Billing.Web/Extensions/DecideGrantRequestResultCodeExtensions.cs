using Corely.Billing.GrantRequests.Models;

namespace Corely.Billing.Web.Extensions;

internal static class DecideGrantRequestResultCodeExtensions
{
    extension(DecideGrantRequestResultCode code)
    {
        public string? ErrorMessage(string action, string message) =>
            code switch
            {
                DecideGrantRequestResultCode.Success => null,
                DecideGrantRequestResultCode.UnauthorizedError =>
                    $"You are not allowed to {action} grant requests.",
                DecideGrantRequestResultCode.NotFoundError => "That request no longer exists.",
                DecideGrantRequestResultCode.AlreadyDecidedError =>
                    "Someone else already decided that request.",
                _ => string.IsNullOrWhiteSpace(message)
                    ? $"Could not {action} the request."
                    : message,
            };
    }
}
