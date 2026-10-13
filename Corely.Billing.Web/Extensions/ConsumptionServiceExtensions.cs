using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;

namespace Corely.Billing.Web.Extensions;

internal static class ConsumptionServiceExtensions
{
    extension(IConsumptionService consumptionService)
    {
        public async Task<Dictionary<Guid, long>?> UsedByGrantAsync(
            Guid accountId,
            IReadOnlyCollection<Grant> grants
        )
        {
            if (grants.Count == 0)
                return [];

            var totals = await consumptionService.GetGrantConsumptionTotalsAsync(
                accountId,
                [.. grants.Select(g => g.GrantId)]
            );
            return totals.ResultCode == RetrieveResultCode.Success
                ? (totals.Item ?? []).ToDictionary(t => t.GrantId, t => t.TotalConsumedQuantity)
                : null;
        }
    }
}
