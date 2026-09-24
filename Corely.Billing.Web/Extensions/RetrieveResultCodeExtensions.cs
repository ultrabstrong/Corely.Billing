using Corely.Billing.Models;

namespace Corely.Billing.Web.Extensions;

internal static class RetrieveResultCodeExtensions
{
    extension(RetrieveResultCode code)
    {
        public string ErrorMessage(string action, string message) =>
            code == RetrieveResultCode.UnauthorizedError ? $"You are not allowed to {action}."
            : string.IsNullOrWhiteSpace(message) ? $"Could not {action}."
            : message;
    }
}
