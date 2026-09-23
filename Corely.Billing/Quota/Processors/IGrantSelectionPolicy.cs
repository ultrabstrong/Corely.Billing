using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.Quota.Processors;

internal interface IGrantSelectionPolicy
{
    GrantSplit Split(List<Grant> grants, List<GrantTotalConsumptions> grantTotals, long quantity);
}
