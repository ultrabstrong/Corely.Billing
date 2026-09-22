using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Mappers;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Serialization;
using Corely.Billing.Usage;
using Corely.Billing.Validators;
using Corely.Common.Extensions;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Grants.Processors;

internal class GrantProcessor(
    IRepo<GrantEntity> grantRepo,
    IValidationProvider validationProvider,
    ILogger<GrantProcessor> logger
) : IGrantProcessor
{
    private readonly IRepo<GrantEntity> _grantRepo = grantRepo.ThrowIfNull(nameof(grantRepo));
    private readonly IValidationProvider _validationProvider = validationProvider.ThrowIfNull(
        nameof(validationProvider)
    );
    private readonly ILogger<GrantProcessor> _logger = logger.ThrowIfNull(nameof(logger));

    public async Task<CreateGrantResult> CreateGrantAsync(
        CreateGrantRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var grant = request.ToGrant();
        var validation = _validationProvider.ValidateAndLog(grant);
        if (!validation.IsValid)
        {
            return new CreateGrantResult(
                CreateGrantResultCode.ValidationError,
                validation.Message,
                Guid.Empty
            );
        }

        await _grantRepo.CreateAsync(grant.ToEntity(), ct);

        _logger.LogInformation(
            "Grant {GrantId} created for AccountId {AccountId}",
            grant.GrantId,
            grant.AccountId
        );
        return new CreateGrantResult(CreateGrantResultCode.Success, string.Empty, grant.GrantId);
    }

    public async Task<ModifyResult> UpdateGrantAsync(
        UpdateGrantRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var entity = await _grantRepo.GetAsync(
            e => e.AccountId == request.AccountId && e.GrantId == request.GrantId,
            cancellationToken: ct
        );
        if (entity is null)
        {
            _logger.LogInformation(
                "Grant {GrantId} not found for AccountId {AccountId}",
                request.GrantId,
                request.AccountId
            );
            return new ModifyResult(ModifyResultCode.NotFoundError, "Grant not found");
        }

        var grant = request.ApplyTo(entity);
        var validation = _validationProvider.ValidateAndLog(grant);
        if (!validation.IsValid)
            return new ModifyResult(ModifyResultCode.ValidationError, validation.Message);

        entity.Unit = grant.Unit;
        entity.Quantity = grant.Quantity;
        entity.ValidFromUtc = grant.ValidFromUtc;
        entity.ValidToUtc = grant.ValidToUtc;
        entity.TagsJson = TagSerializer.Serialize(grant.Tags);

        await _grantRepo.UpdateAsync(entity, ct);
        return new ModifyResult(ModifyResultCode.Success, string.Empty);
    }

    public async Task<DeleteGrantResult> DeleteGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    )
    {
        var entity = await _grantRepo.GetAsync(
            e => e.AccountId == accountId && e.GrantId == grantId,
            cancellationToken: ct
        );
        if (entity is null)
        {
            _logger.LogInformation(
                "Grant {GrantId} not found for AccountId {AccountId}",
                grantId,
                accountId
            );
            return new DeleteGrantResult(DeleteGrantResultCode.NotFoundError, "Grant not found");
        }

        await _grantRepo.DeleteAsync(entity, ct);
        return new DeleteGrantResult(DeleteGrantResultCode.Success, string.Empty);
    }

    public async Task<RetrieveSingleResult<Grant>> GetGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    )
    {
        var entity = await _grantRepo.GetAsync(
            e => e.AccountId == accountId && e.GrantId == grantId,
            cancellationToken: ct
        );

        return entity is null
            ? new RetrieveSingleResult<Grant>(
                RetrieveResultCode.NotFoundError,
                "Grant not found",
                null
            )
            : new RetrieveSingleResult<Grant>(
                RetrieveResultCode.Success,
                string.Empty,
                entity.ToModel()
            );
    }

    public Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        return ListQueryHelper.ExecuteListAsync(
            _grantRepo,
            e => e.AccountId == request.AccountId,
            request.Filter,
            request.Order,
            q => q.OrderByDescending(e => e.ValidFromUtc).ThenBy(e => e.GrantId),
            request.Skip,
            request.Take,
            e => e.ToModel(),
            ct
        );
    }

    public async Task<List<Grant>> ListActiveGrantsAsync(
        Guid accountId,
        UsageOperation operation,
        UsageUnit unit,
        DateTime atUtc,
        CancellationToken ct = default
    )
    {
        var entities = await _grantRepo.ListAsync(
            e =>
                e.AccountId == accountId
                && e.Operation == operation
                && e.Unit == unit
                && e.ValidFromUtc <= atUtc
                && e.ValidToUtc >= atUtc,
            cancellationToken: ct
        );

        return [.. entities.Select(e => e.ToModel())];
    }
}
