using System.Linq.Expressions;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Mappers;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Registrations.Processors;
using Corely.Billing.Serialization;
using Corely.Billing.Usage;
using Corely.Billing.Validators;
using Corely.Common.Extensions;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Grants.Processors;

internal class GrantProcessor(
    IRepo<GrantEntity> grantRepo,
    IConsumptionReportProcessor consumptionReportProcessor,
    IRegistrationProcessor registrationProcessor,
    IUsageVocabulary vocabulary,
    IValidationProvider validationProvider,
    ILogger<GrantProcessor> logger
) : IGrantProcessor
{
    private readonly IRepo<GrantEntity> _grantRepo = grantRepo.ThrowIfNull(nameof(grantRepo));
    private readonly IConsumptionReportProcessor _consumptionReportProcessor =
        consumptionReportProcessor.ThrowIfNull(nameof(consumptionReportProcessor));
    private readonly IRegistrationProcessor _registrationProcessor =
        registrationProcessor.ThrowIfNull(nameof(registrationProcessor));
    private readonly IUsageVocabulary _vocabulary = vocabulary.ThrowIfNull(nameof(vocabulary));
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

        var definition = _vocabulary.Definition(request.Operation);
        if (
            definition is not null
            && !definition.Declares(request.Kind, request.Unit ?? definition.Unit)
        )
        {
            return new CreateGrantResult(
                CreateGrantResultCode.DeclarationMismatchError,
                $"Operation '{request.Operation}' is declared as {definition.Kind}"
                    + (definition.Unit is { } unit ? $" in '{unit}'" : " with no unit")
                    + $", not {request.Kind}"
                    + (request.Unit is { } asked ? $" in '{asked}'" : string.Empty)
                    + ".",
                Guid.Empty
            );
        }

        var grant = request.ToGrant(definition?.Unit);
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

        if (entity.Kind == GrantKind.Capacity)
            await _registrationProcessor.ReleaseAllOnGrantAsync(accountId, grantId, ct);

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
        if (entity is null)
        {
            return new RetrieveSingleResult<Grant>(
                RetrieveResultCode.NotFoundError,
                "Grant not found",
                null
            );
        }

        var grant = entity.ToModel();
        await WithStateAsync([grant], ct);
        return new RetrieveSingleResult<Grant>(RetrieveResultCode.Success, string.Empty, grant);
    }

    public async Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        List<Guid> accountIds = [.. request.AccountIds];
        List<Guid>? authorizedIds = authorizedResourceIds is null
            ? null
            : [.. authorizedResourceIds];
        var kind = request.Kind;
        var operation = request.Operation;
        var activeAt = request.ActiveAtUtc;
        Expression<Func<GrantEntity, bool>> scope = e =>
            accountIds.Contains(e.AccountId)
            && (authorizedIds == null || authorizedIds.Contains(e.GrantId))
            && (kind == null || e.Kind == kind)
            && (operation == null || e.Operation == operation)
            && (
                activeAt == null
                || (!e.Disabled && e.ValidFromUtc <= activeAt && e.ValidToUtc >= activeAt)
            );

        var result = await ListQueryHelper.ExecuteListAsync(
            _grantRepo,
            scope,
            request.Filter,
            request.Order,
            q => q.OrderByDescending(e => e.ValidFromUtc).ThenBy(e => e.GrantId),
            request.Skip,
            request.Take,
            e => e.ToModel(),
            ct
        );
        if (result.Data is { } page)
            await WithStateAsync(page.Items, ct);
        return result;
    }

    public async Task<ModifyResult> SetGrantDisabledAsync(
        SetGrantDisabledRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var changed = await _grantRepo.ExecuteUpdateAsync(
            e => e.AccountId == request.AccountId && e.GrantId == request.GrantId,
            set => set.SetProperty(e => e.Disabled, request.Disabled),
            ct
        );
        if (changed == 0)
        {
            _logger.LogInformation(
                "Grant {GrantId} not found for AccountId {AccountId}",
                request.GrantId,
                request.AccountId
            );
            return new ModifyResult(ModifyResultCode.NotFoundError, "Grant not found");
        }

        _logger.LogInformation(
            "Grant {GrantId} for AccountId {AccountId} set Disabled {Disabled}",
            request.GrantId,
            request.AccountId,
            request.Disabled
        );
        return new ModifyResult(ModifyResultCode.Success, string.Empty);
    }

    public async Task<List<Grant>> ListActiveGrantsAsync(
        Guid accountId,
        UsageOperation operation,
        GrantKind kind,
        DateTime atUtc,
        IReadOnlyCollection<Guid>? heldGrantIds = null,
        CancellationToken ct = default
    )
    {
        List<Guid> held = [.. heldGrantIds ?? []];
        var entities = await _grantRepo.ListAsync(
            e =>
                e.AccountId == accountId
                && e.Operation == operation
                && e.Kind == kind
                && e.ValidFromUtc <= atUtc
                && e.ValidToUtc >= atUtc
                && (!e.Disabled || held.Contains(e.GrantId)),
            cancellationToken: ct
        );

        return [.. entities.Select(e => e.ToModel())];
    }

    private async Task WithStateAsync(IReadOnlyList<Grant> grants, CancellationToken ct)
    {
        var consumables = grants.Where(g => g.Kind == GrantKind.Consumable).ToList();
        foreach (var account in consumables.GroupBy(g => g.AccountId))
        {
            var used = (
                await _consumptionReportProcessor.GetGrantConsumptionTotalsAsync(
                    account.Key,
                    [.. account.Select(g => g.GrantId)],
                    ct
                )
            ).ToDictionary(t => t.GrantId, t => t.TotalConsumedQuantity);
            foreach (var grant in account)
                grant.WithUse(used.GetValueOrDefault(grant.GrantId));
        }

        var capacities = grants.Where(g => g.Kind == GrantKind.Capacity).ToList();
        if (capacities.Count == 0)
            return;

        var inUse = await _registrationProcessor.CountInUseAsync(
            [.. capacities.Select(g => g.GrantId)],
            ct
        );
        foreach (var grant in capacities)
            grant.InUse = inUse.GetValueOrDefault(grant.GrantId);
    }
}
