using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;

namespace Corely.Billing.Quota.Models;

internal sealed record QuotaContext(List<Grant> Grants, List<GrantTotalConsumptions> Totals);
