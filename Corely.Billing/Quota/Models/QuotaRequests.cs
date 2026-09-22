using Corely.Billing;

namespace Corely.Billing.Quota.Models;

/// <summary>
/// Everything needed to hold quota for a piece of work, before doing it.
/// </summary>
/// <remarks>
/// A record rather than loose parameters. Account, operation and unit have no natural order at a call
/// site, and two of them are enums that would happily bind the wrong way round without the compiler
/// noticing.
/// </remarks>
/// <param name="Quantity">
/// What the work is expected to cost. The floor is a legitimate answer where nothing knows the real
/// number yet -- settlement corrects it.
/// </param>
public sealed record ReserveQuotaRequest(
    Guid AccountId,
    UsageOperation Operation,
    UsageUnit Unit,
    long Quantity,
    string Provider,
    Guid? UserId = null,
    IReadOnlyDictionary<string, string>? Tags = null
);

/// <param name="ActualQuantity">What the work turned out to cost.</param>
public sealed record SettleQuotaRequest(
    Guid AccountId,
    UsageOperation Operation,
    UsageUnit Unit,
    long ActualQuantity
);

public sealed record ReleaseQuotaRequest(Guid AccountId, UsageOperation Operation, UsageUnit Unit);
