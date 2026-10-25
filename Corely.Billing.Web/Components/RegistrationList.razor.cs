using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Components;

public partial class RegistrationList
{
    private IGrantActionGate ActionGate => ScopedServices.GetRequiredService<IGrantActionGate>();

    private IQuotaService QuotaService => ScopedServices.GetRequiredService<IQuotaService>();

    private IGrantService GrantService => ScopedServices.GetRequiredService<IGrantService>();

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter, EditorRequired]
    public UsageOperation Operation { get; set; }

    [Parameter]
    public Guid? GrantId { get; set; }

    private List<Registration> _registrations = [];
    private Dictionary<Guid, Grant> _grants = [];
    private List<Grant> _active = [];
    private readonly Dictionary<Guid, Guid> _moveTo = [];
    private Guid _moveAllTo;
    private bool _loading = true;
    private bool _working;
    private string? _error;

    private DateTime Now => TimeProvider.GetUtcNow().UtcDateTime;

    private bool OneGrant => GrantId is not null;

    public Task RefreshAsync() => LoadAsync();

    protected override Task OnParametersSetSignedInAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        var registrations = await QuotaService.ListRegistrationsAsync(
            new ListRegistrationsRequest(
                [AccountId],
                Operation: Operation,
                GrantId: GrantId,
                Take: int.MaxValue
            )
        );
        _registrations = registrations.Data?.Items ?? [];
        _error =
            registrations.ResultCode == RetrieveResultCode.Success
                ? null
                : registrations.ResultCode.ErrorMessage(
                    "view registrations",
                    registrations.Message
                );

        var grants = await GrantService.ListGrantsAsync(
            new ListGrantsRequest(
                [AccountId],
                Kind: GrantKind.Capacity,
                Operation: Operation,
                Take: int.MaxValue
            )
        );
        _grants = (grants.Data?.Items ?? []).ToDictionary(g => g.GrantId);
        _active = [.. _grants.Values.Where(g => g.IsActiveAt(Now))];

        _moveTo.Clear();
        foreach (var registration in _registrations)
        {
            if (Targets(registration.GrantId).FirstOrDefault() is { } target)
                _moveTo[registration.RegistrationId] = target.GrantId;
        }
        _moveAllTo = GrantId is { } grantId
            ? Targets(grantId).FirstOrDefault()?.GrantId ?? Guid.Empty
            : Guid.Empty;
        _loading = false;
    }

    private IEnumerable<Grant> Targets(Guid fromGrantId) =>
        _active.Where(g => g.GrantId != fromGrantId);

    private string GrantText(Guid grantId) =>
        _grants.TryGetValue(grantId, out var grant)
            ? grant.HeadlineAndWindow(Vocabulary)
            : "A grant that no longer exists";

    private Guid MoveTarget(Registration registration) =>
        _moveTo.GetValueOrDefault(registration.RegistrationId);

    private void SetMoveTarget(Registration registration, string? value)
    {
        if (Guid.TryParse(value, out var target))
            _moveTo[registration.RegistrationId] = target;
    }

    private async Task ReleaseAsync(Registration registration)
    {
        _working = true;
        var result = await QuotaService.ReleaseRegistrationAsync(
            AccountId,
            registration.RegistrationId
        );
        _working = false;
        await LoadAsync();
        _error = result.ResultCode switch
        {
            ModifyResultCode.Success => null,
            ModifyResultCode.UnauthorizedError => "You are not allowed to release registrations.",
            _ => result.Message,
        };
    }

    private Task MoveAsync(Registration registration) =>
        MoveAsync(registration.GrantId, MoveTarget(registration), [registration.RegistrationId]);

    private Task MoveAllAsync() => MoveAsync(GrantId!.Value, _moveAllTo, null);

    private async Task MoveAsync(Guid fromGrantId, Guid toGrantId, List<Guid>? registrationIds)
    {
        _working = true;
        var result = await QuotaService.MoveRegistrationsAsync(
            new MoveRegistrationsRequest(AccountId, fromGrantId, toGrantId, registrationIds)
        );
        _working = false;
        await LoadAsync();
        _error = result.ResultCode switch
        {
            MoveRegistrationsResultCode.Success => null,
            MoveRegistrationsResultCode.UnauthorizedError =>
                "You are not allowed to move registrations.",
            _ => result.Message,
        };
    }
}
