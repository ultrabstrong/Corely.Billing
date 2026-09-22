using Corely.Billing;
using Corely.Billing.Grants.DataAccess;
using Corely.Billing.Grants.Mappers;
using Corely.Billing.Grants.Models;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Grants.Services;

internal class GrantWriter(
    IRepo<GrantEntity> grantRepository,
    IUsageVocabulary usageVocabulary,
    ILogger<GrantWriter> logger
) : IGrantWriter
{
    public async Task<SaveGrantResult> SaveAsync(Grant grant, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(grant);

        if (UnknownUsage(grant) is { } unknown)
        {
            logger.LogError(
                "Refusing to save grant {GrantId} for account {AccountId}: {Unknown}",
                grant.GrantId,
                grant.AccountId,
                unknown
            );
            return new SaveGrantResult(SaveGrantResultCode.UnknownUsage, unknown);
        }

        // Idempotent by (AccountId, GrantId)
        var exists = await grantRepository.AnyAsync(
            e => e.AccountId == grant.AccountId && e.GrantId == grant.GrantId,
            ct
        );
        if (exists)
        {
            logger.LogTrace(
                "Grant already exists. Skipping save. AccountId={AccountId}, GrantId={GrantId}",
                grant.AccountId,
                grant.GrantId
            );
            return new SaveGrantResult(SaveGrantResultCode.Success, null);
        }
        await grantRepository.CreateAsync(grant.ToEntity(), ct);
        return new SaveGrantResult(SaveGrantResultCode.Success, null);
    }

    public async Task<UpdateGrantResult> UpdateAsync(Grant grant, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(grant);

        if (UnknownUsage(grant) is { } unknown)
        {
            logger.LogError(
                "Refusing to update grant {GrantId} for account {AccountId}: {Unknown}",
                grant.GrantId,
                grant.AccountId,
                unknown
            );
            return new UpdateGrantResult(UpdateGrantResultCode.UnknownUsage, unknown);
        }

        var entity = await grantRepository.GetAsync(
            e => e.AccountId == grant.AccountId && e.GrantId == grant.GrantId,
            cancellationToken: ct
        );

        if (entity is null)
            return new UpdateGrantResult(
                UpdateGrantResultCode.NotFound,
                $"Grant not found. AccountId={grant.AccountId}, GrantId={grant.GrantId}"
            );

        entity.Quantity = grant.Quantity;
        entity.Unit = grant.Unit;
        entity.Operation = grant.Operation;
        entity.ValidFromUtc = grant.ValidFromUtc;
        entity.ValidToUtc = grant.ValidToUtc;
        entity.TagsJson = grant.Tags is null
            ? null
            : System.Text.Json.JsonSerializer.Serialize(grant.Tags);

        await grantRepository.UpdateAsync(entity, ct);
        return new UpdateGrantResult(UpdateGrantResultCode.Success, null);
    }

    public async Task<DeleteGrantResult> DeleteAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    )
    {
        var entity = await grantRepository.GetAsync(
            e => e.AccountId == accountId && e.GrantId == grantId,
            cancellationToken: ct
        );

        if (entity is null)
            return new DeleteGrantResult(DeleteGrantResultCode.NotFound, null);

        await grantRepository.DeleteAsync(entity, ct);
        return new DeleteGrantResult(DeleteGrantResultCode.Success, null);
    }

    private string? UnknownUsage(Grant grant) =>
        (usageVocabulary.Knows(grant.Operation), usageVocabulary.Knows(grant.Unit)) switch
        {
            (false, _) => $"Operation '{grant.Operation}' is not a registered usage operation.",
            (_, false) => $"Unit '{grant.Unit}' is not a registered usage unit.",
            _ => null,
        };
}
