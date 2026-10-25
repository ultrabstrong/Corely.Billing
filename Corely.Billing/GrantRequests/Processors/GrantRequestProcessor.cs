using System.Linq.Expressions;
using Corely.Billing.GrantRequests.Entities;
using Corely.Billing.GrantRequests.Mappers;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Models;
using Corely.Billing.Usage;
using Corely.Billing.Validators;
using Corely.Common.Extensions;
using Corely.DataAccess.Interfaces.Repos;
using Corely.DataAccess.Interfaces.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.GrantRequests.Processors;

internal class GrantRequestProcessor(
    IRepo<GrantRequestEntity> grantRequestRepo,
    IGrantProcessor grantProcessor,
    IUsageVocabulary vocabulary,
    IUnitOfWorkProvider uowProvider,
    IValidationProvider validationProvider,
    TimeProvider timeProvider,
    ILogger<GrantRequestProcessor> logger
) : IGrantRequestProcessor
{
    private readonly IRepo<GrantRequestEntity> _grantRequestRepo = grantRequestRepo.ThrowIfNull(
        nameof(grantRequestRepo)
    );
    private readonly IGrantProcessor _grantProcessor = grantProcessor.ThrowIfNull(
        nameof(grantProcessor)
    );
    private readonly IUsageVocabulary _vocabulary = vocabulary.ThrowIfNull(nameof(vocabulary));
    private readonly IUnitOfWorkProvider _uowProvider = uowProvider.ThrowIfNull(
        nameof(uowProvider)
    );
    private readonly IValidationProvider _validationProvider = validationProvider.ThrowIfNull(
        nameof(validationProvider)
    );
    private readonly TimeProvider _timeProvider = timeProvider.ThrowIfNull(nameof(timeProvider));
    private readonly ILogger<GrantRequestProcessor> _logger = logger.ThrowIfNull(nameof(logger));

    public async Task<RequestGrantResult> RequestGrantAsync(
        RequestGrantRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        if (_vocabulary.Definition(request.Operation) is not { } definition)
        {
            return new RequestGrantResult(
                RequestGrantResultCode.ValidationError,
                $"Operation '{request.Operation}' is not a registered usage operation.",
                Guid.Empty
            );
        }

        var grantRequest = request.ToGrantRequest(
            definition,
            _timeProvider.GetUtcNow().UtcDateTime
        );
        var validation = _validationProvider.ValidateAndLog(grantRequest);
        if (!validation.IsValid)
        {
            return new RequestGrantResult(
                RequestGrantResultCode.ValidationError,
                validation.Message,
                Guid.Empty
            );
        }

        var alreadyPending = await _grantRequestRepo.AnyAsync(
            e =>
                e.AccountId == request.AccountId
                && e.Operation == request.Operation
                && e.Status == GrantRequestStatus.Pending,
            ct
        );
        if (alreadyPending)
        {
            return new RequestGrantResult(
                RequestGrantResultCode.AlreadyPendingError,
                "A request for this operation is already pending",
                Guid.Empty
            );
        }

        await _grantRequestRepo.CreateAsync(grantRequest.ToEntity(), ct);

        _logger.LogInformation(
            "Grant request {GrantRequestId} created for AccountId {AccountId}",
            grantRequest.GrantRequestId,
            grantRequest.AccountId
        );
        return new RequestGrantResult(
            RequestGrantResultCode.Success,
            string.Empty,
            grantRequest.GrantRequestId
        );
    }

    public async Task<RetrieveSingleResult<GrantRequest>> GetGrantRequestAsync(
        Guid accountId,
        Guid grantRequestId,
        CancellationToken ct = default
    )
    {
        var entity = await _grantRequestRepo.GetAsync(
            e => e.AccountId == accountId && e.GrantRequestId == grantRequestId,
            cancellationToken: ct
        );

        return entity is null
            ? new RetrieveSingleResult<GrantRequest>(
                RetrieveResultCode.NotFoundError,
                "Grant request not found",
                null
            )
            : new RetrieveSingleResult<GrantRequest>(
                RetrieveResultCode.Success,
                string.Empty,
                entity.ToModel()
            );
    }

    public Task<RetrieveListResult<GrantRequest>> ListGrantRequestsAsync(
        ListGrantRequestsRequest request,
        IReadOnlySet<Guid>? authorizedResourceIds = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        List<Guid> accountIds = [.. request.AccountIds];
        List<Guid>? authorizedIds = authorizedResourceIds is null
            ? null
            : [.. authorizedResourceIds];
        var operation = request.Operation;
        var status = request.Status;
        Expression<Func<GrantRequestEntity, bool>> scope = e =>
            accountIds.Contains(e.AccountId)
            && (authorizedIds == null || authorizedIds.Contains(e.GrantRequestId))
            && (operation == null || e.Operation == operation)
            && (status == null || e.Status == status);

        return ListQueryHelper.ExecuteListAsync(
            _grantRequestRepo,
            scope,
            request.Filter,
            request.Order,
            q =>
                q.OrderBy(e => e.Status == GrantRequestStatus.Pending ? 0 : 1)
                    .ThenByDescending(e => e.RequestedUtc)
                    .ThenBy(e => e.GrantRequestId),
            request.Skip,
            request.Take,
            e => e.ToModel(),
            ct
        );
    }

    public async Task<ApproveGrantRequestResult> ApproveGrantRequestAsync(
        ApproveGrantRequestRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var entity = await FindAsync(request.AccountId, request.GrantRequestId, ct);
        if (Undecidable(entity) is { } refused)
            return new ApproveGrantRequestResult(refused.Code, refused.Message, Guid.Empty);

        await _uowProvider.BeginAsync(ct);
        var committing = false;
        try
        {
            var grant = await _grantProcessor.CreateGrantAsync(
                entity!.ToCreateGrantRequest(request),
                ct
            );
            if (grant.ResultCode != CreateGrantResultCode.Success)
            {
                return new ApproveGrantRequestResult(
                    DecideGrantRequestResultCode.ValidationError,
                    grant.Message,
                    Guid.Empty
                );
            }

            var decidedUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var claimed = await ClaimAsync(
                request.AccountId,
                request.GrantRequestId,
                set =>
                    set.SetProperty(e => e.Status, GrantRequestStatus.Approved)
                        .SetProperty(e => e.GrantId, grant.CreatedId)
                        .SetProperty(e => e.DecidedUtc, decidedUtc)
                        .SetProperty(e => e.DecidedByUserId, request.DecidedByUserId),
                ct
            );
            if (!claimed)
            {
                return new ApproveGrantRequestResult(
                    DecideGrantRequestResultCode.AlreadyDecidedError,
                    "Grant request was already decided",
                    Guid.Empty
                );
            }

            committing = true;
            await _uowProvider.CommitAsync(ct);

            _logger.LogInformation(
                "Grant request {GrantRequestId} approved as Grant {GrantId} for AccountId {AccountId}",
                request.GrantRequestId,
                grant.CreatedId,
                request.AccountId
            );
            return new ApproveGrantRequestResult(
                DecideGrantRequestResultCode.Success,
                string.Empty,
                grant.CreatedId
            );
        }
        finally
        {
            if (!committing)
                await _uowProvider.RollbackAsync(CancellationToken.None);
        }
    }

    public async Task<DeclineGrantRequestResult> DeclineGrantRequestAsync(
        DeclineGrantRequestRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var validation = _validationProvider.ValidateAndLog(request);
        if (!validation.IsValid)
        {
            return new DeclineGrantRequestResult(
                DecideGrantRequestResultCode.ValidationError,
                validation.Message
            );
        }

        var entity = await FindAsync(request.AccountId, request.GrantRequestId, ct);
        if (Undecidable(entity) is { } refused)
            return new DeclineGrantRequestResult(refused.Code, refused.Message);

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var decidedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var claimed = await ClaimAsync(
            request.AccountId,
            request.GrantRequestId,
            set =>
                set.SetProperty(e => e.Status, GrantRequestStatus.Declined)
                    .SetProperty(e => e.DecisionReason, reason)
                    .SetProperty(e => e.DecidedUtc, decidedUtc)
                    .SetProperty(e => e.DecidedByUserId, request.DecidedByUserId),
            ct
        );
        if (!claimed)
        {
            return new DeclineGrantRequestResult(
                DecideGrantRequestResultCode.AlreadyDecidedError,
                "Grant request was already decided"
            );
        }

        _logger.LogInformation(
            "Grant request {GrantRequestId} declined for AccountId {AccountId}",
            request.GrantRequestId,
            request.AccountId
        );
        return new DeclineGrantRequestResult(DecideGrantRequestResultCode.Success, string.Empty);
    }

    private Task<GrantRequestEntity?> FindAsync(
        Guid accountId,
        Guid grantRequestId,
        CancellationToken ct
    ) =>
        _grantRequestRepo.GetAsync(
            e => e.AccountId == accountId && e.GrantRequestId == grantRequestId,
            cancellationToken: ct
        );

    private static (DecideGrantRequestResultCode Code, string Message)? Undecidable(
        GrantRequestEntity? entity
    ) =>
        entity switch
        {
            null => (DecideGrantRequestResultCode.NotFoundError, "Grant request not found"),
            { Status: not GrantRequestStatus.Pending } => (
                DecideGrantRequestResultCode.AlreadyDecidedError,
                "Grant request was already decided"
            ),
            _ => null,
        };

    private async Task<bool> ClaimAsync(
        Guid accountId,
        Guid grantRequestId,
        Action<IUpdateSetters<GrantRequestEntity>> decide,
        CancellationToken ct
    ) =>
        await _grantRequestRepo.ExecuteUpdateAsync(
            e =>
                e.AccountId == accountId
                && e.GrantRequestId == grantRequestId
                && e.Status == GrantRequestStatus.Pending,
            decide,
            ct
        ) == 1;
}
