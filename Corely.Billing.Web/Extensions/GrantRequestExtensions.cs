using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.Extensions;

internal static class GrantRequestExtensions
{
    extension(GrantRequest request)
    {
        public string Asked(IUsageVocabulary vocabulary) =>
            request.Unit is { } unit && request.Quantity is { } quantity
                ? UsageText.Count(quantity, vocabulary.DisplayName(unit))
                : vocabulary.DisplayName(request.Operation);

        public string RequestedText(DateTime utcNow) =>
            $"Requested {(utcNow - request.RequestedUtc).AgoText()}";
    }
}
