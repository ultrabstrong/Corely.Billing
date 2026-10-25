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
    public Grant Grant { get; set; } = null!;

    private List<Registration> _registrations = [];
    private List<Grant> _targets = [];
    private Guid _targetId;
    private bool _loading = true;
    private bool _working;
    private string? _error;

    protected override Task OnParametersSetSignedInAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loading = true;
        var registrations = await QuotaService.ListRegistrationsAsync(
            new ListRegistrationsRequest([AccountId], GrantId: Grant.GrantId, Take: int.MaxValue)
        );
        _registrations = registrations.Data?.Items ?? [];
        if (registrations.ResultCode != RetrieveResultCode.Success)
            _error = registrations.ResultCode.ErrorMessage(
                "view registrations",
                registrations.Message
            );

        var grants = await GrantService.ListGrantsAsync(
            new ListGrantsRequest(
                [AccountId],
                Kind: GrantKind.Capacity,
                Operation: Grant.Operation,
                ActiveAtUtc: TimeProvider.GetUtcNow().UtcDateTime,
                Take: int.MaxValue
            )
        );
        _targets = [.. (grants.Data?.Items ?? []).Where(g => g.GrantId != Grant.GrantId)];
        _targetId = _targets.FirstOrDefault()?.GrantId ?? Guid.Empty;
        _loading = false;
    }

    private async Task ReleaseAsync(Registration registration)
    {
        _working = true;
        var result = await QuotaService.ReleaseRegistrationAsync(
            AccountId,
            registration.RegistrationId
        );
        _working = false;
        _error = result.ResultCode switch
        {
            ModifyResultCode.Success => null,
            ModifyResultCode.UnauthorizedError => "You are not allowed to release registrations.",
            _ => result.Message,
        };
        await LoadAsync();
    }

    private async Task MoveAllAsync()
    {
        _working = true;
        var result = await QuotaService.MoveRegistrationsAsync(
            new MoveRegistrationsRequest(AccountId, Grant.GrantId, _targetId)
        );
        _working = false;
        _error = result.ResultCode switch
        {
            MoveRegistrationsResultCode.Success => null,
            MoveRegistrationsResultCode.UnauthorizedError =>
                "You are not allowed to move registrations.",
            _ => result.Message,
        };
        await LoadAsync();
    }
}
