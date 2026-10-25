using Corely.Billing.Usage;

namespace Corely.Billing.Registrations.Models;

public record ListRegistrationsRequest(
    IReadOnlyList<Guid> AccountIds,
    UsageOperation? Operation = null,
    Guid? GrantId = null,
    bool IncludeReleased = false,
    int Skip = 0,
    int Take = 25
);
