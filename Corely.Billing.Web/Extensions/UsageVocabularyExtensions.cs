using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Usage;

namespace Corely.Billing.Web.Extensions;

internal static class UsageVocabularyExtensions
{
    extension(IUsageVocabulary vocabulary)
    {
        public IReadOnlyList<UsageOperationDefinition> Unrequested(
            IEnumerable<GrantRequest> pending
        )
        {
            var taken = pending.Select(r => r.Operation).ToHashSet();
            return [.. vocabulary.Operations.Where(o => !taken.Contains(o.Operation))];
        }

        public IReadOnlyList<IGrouping<GrantKind, UsageOperationDefinition>> ByKind(
            IEnumerable<UsageOperationDefinition>? operations = null
        ) => [.. (operations ?? vocabulary.Operations).GroupBy(o => o.Kind).OrderBy(g => g.Key)];
    }
}
