using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Mappers;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Registrations.Entities;
using Corely.Billing.Registrations.Mappers;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Usage;
using Corely.Billing.Validators;
using Corely.Common.Extensions;
using Corely.DataAccess.Interfaces.Repos;
using Corely.DataAccess.Interfaces.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Registrations.Processors;

internal class RegistrationProcessor(
    IRepo<RegistrationEntity> registrationRepo,
    IRepo<GrantEntity> grantRepo,
    IUsageVocabulary vocabulary,
    IRegistrationSelectionPolicy selectionPolicy,
    IUnitOfWorkProvider uowProvider,
    IValidationProvider validationProvider,
    TimeProvider timeProvider,
    ILogger<RegistrationProcessor> logger
) : IRegistrationProcessor
{
    private readonly IRepo<RegistrationEntity> _registrationRepo = registrationRepo.ThrowIfNull(
        nameof(registrationRepo)
    );
    private readonly IRepo<GrantEntity> _grantRepo = grantRepo.ThrowIfNull(nameof(grantRepo));
    private readonly IUsageVocabulary _vocabulary = vocabulary.ThrowIfNull(nameof(vocabulary));
    private readonly IRegistrationSelectionPolicy _selectionPolicy = selectionPolicy.ThrowIfNull(
        nameof(selectionPolicy)
    );
    private readonly IUnitOfWorkProvider _uowProvider = uowProvider.ThrowIfNull(
        nameof(uowProvider)
    );
    private readonly IValidationProvider _validationProvider = validationProvider.ThrowIfNull(
        nameof(validationProvider)
    );
    private readonly TimeProvider _timeProvider = timeProvider.ThrowIfNull(nameof(timeProvider));
    private readonly ILogger<RegistrationProcessor> _logger = logger.ThrowIfNull(nameof(logger));

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<RegisterResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var validation = _validationProvider.ValidateAndLog(request);
        if (!validation.IsValid)
            return new RegisterResult(RegisterResultCode.ValidationError, validation.Message);

        if (_vocabulary.Definition(request.Operation)?.Kind != GrantKind.Capacity)
        {
            return new RegisterResult(
                RegisterResultCode.WrongKindError,
                $"Operation '{request.Operation}' is not declared as capacity"
            );
        }

        var registration = request.ToEntity(Guid.Empty, UtcNow);

        await _uowProvider.BeginAsync(ct);
        var committing = false;
        try
        {
            var grants = await LockActiveGrantsAsync(request.AccountId, request.Operation, ct);
            if (grants.Count == 0)
            {
                return new RegisterResult(
                    RegisterResultCode.NoActiveGrantError,
                    "No active capacity grant covers this operation"
                );
            }

            if (
                registration.Reference is { } reference
                && await _registrationRepo.AnyAsync(
                    r =>
                        r.AccountId == request.AccountId
                        && r.Operation == request.Operation
                        && r.Reference == reference
                        && r.ReleasedUtc == null,
                    ct
                )
            )
            {
                return new RegisterResult(
                    RegisterResultCode.AlreadyRegisteredError,
                    $"'{reference}' is already registered"
                );
            }

            var grant = _selectionPolicy.Pick(
                grants,
                await CountInUseAsync([.. grants.Select(g => g.GrantId)], ct)
            );
            if (grant is null)
            {
                return new RegisterResult(
                    RegisterResultCode.NoCapacityError,
                    "Every active capacity grant is full"
                );
            }

            registration.GrantId = grant.GrantId;
            await _registrationRepo.CreateAsync(registration, ct);

            committing = true;
            await _uowProvider.CommitAsync(ct);

            _logger.LogInformation(
                "Registration {RegistrationId} holds a seat on grant {GrantId} for AccountId {AccountId}",
                registration.RegistrationId,
                grant.GrantId,
                request.AccountId
            );
            return new RegisterResult(
                RegisterResultCode.Registered,
                string.Empty,
                registration.ToModel(grant, UtcNow)
            );
        }
        finally
        {
            if (!committing)
                await _uowProvider.RollbackAsync(CancellationToken.None);
        }
    }

    public async Task<ModifyResult> ReleaseAsync(
        Guid accountId,
        Guid registrationId,
        CancellationToken ct = default
    )
    {
        var releasedUtc = UtcNow;
        var released = await _registrationRepo.ExecuteUpdateAsync(
            r =>
                r.AccountId == accountId
                && r.RegistrationId == registrationId
                && r.ReleasedUtc == null,
            set => set.SetProperty(r => r.ReleasedUtc, releasedUtc),
            ct
        );
        if (
            released == 0
            && !await _registrationRepo.AnyAsync(
                r => r.AccountId == accountId && r.RegistrationId == registrationId,
                ct
            )
        )
        {
            return new ModifyResult(ModifyResultCode.NotFoundError, "Registration not found");
        }

        return new ModifyResult(ModifyResultCode.Success, string.Empty);
    }

    public async Task<MoveRegistrationsResult> MoveAsync(
        MoveRegistrationsRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        await _uowProvider.BeginAsync(ct);
        var committing = false;
        try
        {
            var from = await _grantRepo.GetAsync(
                g => g.AccountId == request.AccountId && g.GrantId == request.FromGrantId,
                cancellationToken: ct
            );
            var to = await _grantRepo.GetAsync(
                g => g.AccountId == request.AccountId && g.GrantId == request.ToGrantId,
                cancellationToken: ct
            );
            if (from is null || to is null)
                return Moved(MoveRegistrationsResultCode.NotFoundError, "Grant not found");

            if (
                from.Kind != GrantKind.Capacity
                || to.Kind != GrantKind.Capacity
                || from.Operation != to.Operation
            )
            {
                return Moved(
                    MoveRegistrationsResultCode.WrongKindError,
                    "Registrations move only between capacity grants of one operation"
                );
            }

            await LockAsync(request.AccountId, [to.GrantId], ct);

            List<Guid>? ids = request.RegistrationIds is null ? null : [.. request.RegistrationIds];
            var moving = await _registrationRepo.ListAsync(
                r =>
                    r.AccountId == request.AccountId
                    && r.GrantId == from.GrantId
                    && r.ReleasedUtc == null
                    && (ids == null || ids.Contains(r.RegistrationId)),
                cancellationToken: ct
            );

            var target = to.ToModel();
            var inUse = (await CountInUseAsync([to.GrantId], ct)).GetValueOrDefault(to.GrantId);
            if (target.Quantity is { } seats && inUse + moving.Count > seats)
            {
                return Moved(
                    MoveRegistrationsResultCode.NoCapacityError,
                    $"The target grant has {Math.Max(0, seats - inUse)} seats free for {moving.Count}"
                );
            }

            var now = UtcNow;
            List<Registration> moved = [];
            foreach (var registration in moving)
            {
                registration.ReleasedUtc = now;
                await _registrationRepo.UpdateAsync(registration, ct);

                var replacement = registration.MovedTo(to.GrantId, now);
                await _registrationRepo.CreateAsync(replacement, ct);
                moved.Add(replacement.ToModel(target, now));
            }

            committing = true;
            await _uowProvider.CommitAsync(ct);

            _logger.LogInformation(
                "Moved {Count} registrations from grant {FromGrantId} to grant {ToGrantId} for AccountId {AccountId}",
                moved.Count,
                from.GrantId,
                to.GrantId,
                request.AccountId
            );
            return new MoveRegistrationsResult(
                MoveRegistrationsResultCode.Success,
                string.Empty,
                moved
            );
        }
        finally
        {
            if (!committing)
                await _uowProvider.RollbackAsync(CancellationToken.None);
        }
    }

    public async Task<RetrieveListResult<Registration>> ListAsync(
        ListRegistrationsRequest request,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));
        if (request.Skip < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Skip must be non-negative.");
        if (request.Take <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Take must be positive.");

        List<Guid> accountIds = [.. request.AccountIds];
        var operation = request.Operation;
        var grantId = request.GrantId;
        var includeReleased = request.IncludeReleased;

        var page = await _registrationRepo.QueryAsync(
            q =>
                q.Where(r =>
                        accountIds.Contains(r.AccountId)
                        && (operation == null || r.Operation == operation)
                        && (grantId == null || r.GrantId == grantId)
                        && (includeReleased || r.ReleasedUtc == null)
                    )
                    .OrderBy(r => r.ReleasedUtc != null)
                    .ThenByDescending(r => r.RegisteredUtc)
                    .ThenBy(r => r.RegistrationId)
                    .Skip(request.Skip)
                    .Take(request.Take),
            ct
        );
        var total = await _registrationRepo.CountAsync(
            r =>
                accountIds.Contains(r.AccountId)
                && (operation == null || r.Operation == operation)
                && (grantId == null || r.GrantId == grantId)
                && (includeReleased || r.ReleasedUtc == null),
            ct
        );

        List<Guid> grantIds = [.. page.Select(r => r.GrantId).Distinct()];
        var grants = (
            await _grantRepo.ListAsync(g => grantIds.Contains(g.GrantId), cancellationToken: ct)
        ).ToDictionary(g => g.GrantId, g => g.ToModel());

        var now = UtcNow;
        return new RetrieveListResult<Registration>(
            RetrieveResultCode.Success,
            string.Empty,
            PagedResult<Registration>.Create(
                [.. page.Select(r => r.ToModel(grants.GetValueOrDefault(r.GrantId), now))],
                total,
                request.Skip,
                request.Take
            )
        );
    }

    public async Task<Dictionary<Guid, long>> CountInUseAsync(
        IReadOnlyCollection<Guid> grantIds,
        CancellationToken ct = default
    )
    {
        if (grantIds.Count == 0)
            return [];

        List<Guid> ids = [.. grantIds];
        var held = await _registrationRepo.QueryAsync(
            q =>
                q.Where(r => ids.Contains(r.GrantId) && r.ReleasedUtc == null)
                    .Select(r => r.GrantId),
            ct
        );
        return held.GroupBy(id => id).ToDictionary(g => g.Key, g => (long)g.Count());
    }

    private async Task<List<Grant>> LockActiveGrantsAsync(
        Guid accountId,
        UsageOperation operation,
        CancellationToken ct
    )
    {
        var now = UtcNow;
        var entities = await _grantRepo.ListAsync(
            g =>
                g.AccountId == accountId
                && g.Operation == operation
                && g.Kind == GrantKind.Capacity
                && !g.Disabled
                && g.ValidFromUtc <= now
                && g.ValidToUtc >= now,
            cancellationToken: ct
        );
        await LockAsync(accountId, [.. entities.Select(g => g.GrantId)], ct);
        return [.. entities.Select(g => g.ToModel())];
    }

    private Task LockAsync(Guid accountId, List<Guid> grantIds, CancellationToken ct) =>
        grantIds.Count == 0
            ? Task.CompletedTask
            : _grantRepo.ExecuteUpdateAsync(
                g => g.AccountId == accountId && grantIds.Contains(g.GrantId),
                set => set.SetProperty(g => g.Disabled, g => g.Disabled),
                ct
            );

    private static MoveRegistrationsResult Moved(
        MoveRegistrationsResultCode code,
        string message
    ) => new(code, message, []);
}
