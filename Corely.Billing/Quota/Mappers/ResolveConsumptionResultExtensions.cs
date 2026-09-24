using Corely.Billing.Consumption.Models;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.Quota.Mappers;

internal static class ResolveConsumptionResultExtensions
{
    extension(ResolveConsumptionResult result)
    {
        public SettleQuotaResult ToSettleQuotaResult(bool overdrawn) =>
            new(
                result.ResultCode == ResolveConsumptionResultCode.Success
                    ? SettleQuotaResultCode.Success
                    : SettleQuotaResultCode.NotRecordedError,
                result.Message,
                result.SettledQuantity,
                overdrawn
            );
    }
}
