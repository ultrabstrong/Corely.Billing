using Corely.Billing.GrantRequests.Enums;
using Corely.Billing.Usage;
using Corely.Common.Filtering;
using Corely.Common.Filtering.Ordering;

namespace Corely.Billing.GrantRequests.Models;

public record ListGrantRequestsRequest(
    IReadOnlyList<Guid> AccountIds,
    UsageOperation? Operation = null,
    GrantRequestStatus? Status = null,
    FilterBuilder<GrantRequest>? Filter = null,
    OrderBuilder<GrantRequest>? Order = null,
    int Skip = 0,
    int Take = 25
);
