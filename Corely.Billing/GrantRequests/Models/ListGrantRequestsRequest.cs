using Corely.Common.Filtering;
using Corely.Common.Filtering.Ordering;

namespace Corely.Billing.GrantRequests.Models;

public record ListGrantRequestsRequest(
    Guid AccountId,
    FilterBuilder<GrantRequest>? Filter = null,
    OrderBuilder<GrantRequest>? Order = null,
    int Skip = 0,
    int Take = 25
);
