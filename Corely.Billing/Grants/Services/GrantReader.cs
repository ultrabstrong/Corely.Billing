using Corely.Billing;
using Corely.Billing.Grants.DataAccess;
using Corely.Billing.Grants.Mappers;
using Corely.Billing.Grants.Models;
using Corely.DataAccess.Interfaces.Repos;

namespace Corely.Billing.Grants.Services;

internal class GrantReader(IReadonlyRepo<GrantEntity> grantRepo) : IGrantReader
{
    public async Task<GetAllGrantsResult> GetAllGrantsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var grants = await grantRepo.QueryAsync(
            q => q.Where(e => e.AccountId == accountId).OrderByDescending(e => e.ValidFromUtc),
            ct
        );

        return new GetAllGrantsResult(
            GetAllGrantsResultCode.Success,
            null,
            [.. grants.Select(Grant.FromEntity)]
        );
    }

    public async Task<GetGrantByIdResult> GetGrantByIdAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    )
    {
        var entity = await grantRepo.GetAsync(
            e => e.AccountId == accountId && e.GrantId == grantId,
            cancellationToken: ct
        );

        return entity is null
            ? new GetGrantByIdResult(GetGrantByIdResultCode.NotFound, null)
            : new GetGrantByIdResult(
                GetGrantByIdResultCode.Success,
                null,
                Grant.FromEntity(entity)
            );
    }

    public async Task<GetActiveGrantsResult> GetActiveGrantsAsync(
        Guid accountId,
        DateTime atUtc,
        UsageOperation? operation = null,
        UsageUnit? unit = null,
        IReadOnlyDictionary<string, string>? tags = null,
        CancellationToken ct = default
    )
    {
        var grants = await grantRepo.QueryAsync(
            q =>
            {
                q = q.Where(e =>
                    e.AccountId == accountId && e.ValidFromUtc <= atUtc && e.ValidToUtc >= atUtc
                );
                if (operation is not null)
                {
                    q = q.Where(e => e.Operation == operation);
                }
                if (unit is not null)
                {
                    q = q.Where(e => e.Unit == unit);
                }
                return q;
            },
            ct
        );

        return new GetActiveGrantsResult(
            GetActiveGrantsResultCode.Success,
            null,
            [.. grants.Select(Grant.FromEntity)]
        );
    }
}
