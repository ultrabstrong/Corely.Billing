using Corely.Common.Filtering;
using Corely.Common.Filtering.Ordering;

namespace Corely.Billing.Grants.Models;

public record ListGrantsRequest(
    Guid AccountId,
    FilterBuilder<Grant>? Filter = null,
    OrderBuilder<Grant>? Order = null,
    int Skip = 0,
    int Take = 25
);
