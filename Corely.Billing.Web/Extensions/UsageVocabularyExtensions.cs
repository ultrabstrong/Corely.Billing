using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.Extensions;

internal static class UsageVocabularyExtensions
{
    extension(IUsageVocabulary vocabulary)
    {
        public IReadOnlyList<(UsageOperation Operation, UsageUnit Unit)> Unrequested(
            IEnumerable<GrantRequest> pending
        )
        {
            var taken = pending.Select(r => (r.Operation, r.Unit)).ToHashSet();
            return
            [
                .. vocabulary
                    .Operations.SelectMany(o => vocabulary.Units.Select(u => (o.Operation, u.Unit)))
                    .Where(pair => !taken.Contains(pair)),
            ];
        }
    }
}
