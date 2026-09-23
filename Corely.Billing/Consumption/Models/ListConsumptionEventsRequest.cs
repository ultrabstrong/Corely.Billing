using Corely.Billing.Usage;
using Corely.Common.Filtering.Ordering;

namespace Corely.Billing.Consumption.Models;

public record ListConsumptionEventsRequest(
    Guid AccountId,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    IReadOnlyList<UsageUnit>? Units = null,
    IReadOnlyList<UsageOperation>? Operations = null,
    IReadOnlyList<string>? Providers = null,
    IReadOnlyList<Guid>? GrantIds = null,
    ConsumptionEventSortField SortBy = ConsumptionEventSortField.UtcTimestamp,
    SortDirection SortDirection = SortDirection.Descending,
    int Skip = 0,
    int Take = 25
);
