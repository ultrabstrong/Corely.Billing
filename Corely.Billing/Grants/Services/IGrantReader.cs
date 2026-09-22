using Corely.Billing;
using Corely.Billing.Grants.Models;

namespace Corely.Billing.Grants.Services;

public interface IGrantReader
{
    Task<GetAllGrantsResult> GetAllGrantsAsync(Guid accountId, CancellationToken ct = default);

    Task<GetGrantByIdResult> GetGrantByIdAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    );

    Task<GetActiveGrantsResult> GetActiveGrantsAsync(
        Guid accountId,
        DateTime atUtc,
        UsageOperation? operation = null,
        UsageUnit? unit = null,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken ct = default
    );
}
