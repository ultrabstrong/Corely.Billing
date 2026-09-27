using Corely.Billing.Consumption.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.Extensions;

internal static class UsageBreakdownExtensions
{
    extension(UsageBreakdown by)
    {
        public string Label() =>
            by switch
            {
                UsageBreakdown.Provider => "provider",
                UsageBreakdown.Grant => "grant",
                _ => "operation",
            };

        public ConsumptionDimension ToDimension() =>
            by switch
            {
                UsageBreakdown.Provider => ConsumptionDimension.Provider,
                UsageBreakdown.Grant => ConsumptionDimension.Grant,
                _ => ConsumptionDimension.Operation,
            };
    }
}
