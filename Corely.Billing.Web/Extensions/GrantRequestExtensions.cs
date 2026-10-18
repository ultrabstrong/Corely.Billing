using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.Extensions;

internal static class GrantRequestExtensions
{
    extension(GrantRequest request)
    {
        public string Asked(IUsageVocabulary vocabulary) =>
            UsageText.Count(request.Quantity, vocabulary.DisplayName(request.Unit));

        public string RequestedText(DateTime utcNow) =>
            $"Requested {(utcNow - request.RequestedUtc).AgoText()}";
    }
}
