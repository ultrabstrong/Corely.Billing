using Corely.Billing.Grants.Enums;
using Corely.Billing.Usage;
using Corely.Common.Filtering;
using Corely.Common.Filtering.Ordering;

namespace Corely.Billing.Grants.Models;

public record ListGrantsRequest(
    IReadOnlyList<Guid> AccountIds,
    GrantKind? Kind = null,
    UsageOperation? Operation = null,
    DateTime? ActiveAtUtc = null,
    FilterBuilder<Grant>? Filter = null,
    OrderBuilder<Grant>? Order = null,
    int Skip = 0,
    int Take = 25
);
