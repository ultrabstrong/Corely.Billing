using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.Components;

public partial class GrantEditor
{
    [Inject]
    private IGrantActionGate ActionGate { get; set; } = null!;

    [Inject]
    private IGrantService GrantService { get; set; } = null!;

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter]
    public Guid? GrantId { get; set; }

    [Parameter]
    public EventCallback<Guid> OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private bool _loading = true;
    private bool _saving;
    private string? _loadError;
    private string? _error;
    private Grant? _grant;

    private UsageOperation _operation;
    private UsageUnit _unit;
    private long _quantity;
    private bool _unlimited;
    private DateTime _validFromUtc;
    private DateTime _validToUtc;

    private bool IsNew => GrantId is null;

    private string OperationValue
    {
        get => _operation.Value;
        set => _operation = UsageOperation.From(value);
    }

    private string UnitValue
    {
        get => _unit.Value;
        set => _unit = UsageUnit.From(value);
    }

    protected override Task OnParametersSetAsync() => SerializedAsync(LoadAsync);

    private async Task LoadAsync()
    {
        _loading = true;
        _loadError = null;
        _error = null;

        if (IsNew)
        {
            var today = TimeProvider.GetUtcNow().UtcDateTime.Date;
            _operation = Vocabulary.Operations[0].Operation;
            _unit = Vocabulary.Units[0].Unit;
            _quantity = 0;
            _unlimited = false;
            _validFromUtc = today;
            _validToUtc = today.AddYears(1);
            _loading = false;
            return;
        }

        var result = await GrantService.GetGrantAsync(AccountId, GrantId!.Value);
        if (result.ResultCode != RetrieveResultCode.Success || result.Item is null)
        {
            _loadError = result.ResultCode.ErrorMessage(
                "view this grant",
                result.ResultCode == RetrieveResultCode.NotFoundError
                    ? "This grant no longer exists."
                    : result.Message
            );
            _loading = false;
            return;
        }

        _grant = result.Item;
        _operation = _grant.Operation;
        _unit = _grant.Unit;
        _unlimited = _grant.Quantity is null;
        _quantity = _grant.Quantity ?? 0;
        _validFromUtc = _grant.ValidFromUtc;
        _validToUtc = _grant.ValidToUtc;
        _loading = false;
    }

    private Task SaveAsync() => SerializedAsync(SaveCoreAsync);

    private async Task SaveCoreAsync()
    {
        _error = Validate();
        if (_error is not null)
            return;

        _saving = true;
        var quantity = _unlimited ? (long?)null : _quantity;
        var fromUtc = DateTime.SpecifyKind(_validFromUtc, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(_validToUtc, DateTimeKind.Utc);

        var (grantId, error) = IsNew
            ? await CreateAsync(quantity, fromUtc, toUtc)
            : await UpdateAsync(quantity, fromUtc, toUtc);

        _saving = false;
        _error = error;
        if (error is null)
            await OnSaved.InvokeAsync(grantId);
    }

    private string? Validate() =>
        !_unlimited && _quantity < 0 ? "Quantity must be zero or more."
        : _validToUtc <= _validFromUtc ? "Valid to must be after valid from."
        : null;

    private async Task<(Guid, string?)> CreateAsync(
        long? quantity,
        DateTime fromUtc,
        DateTime toUtc
    )
    {
        var result = await GrantService.CreateGrantAsync(
            new CreateGrantRequest(AccountId, _operation, _unit, quantity, fromUtc, toUtc)
        );
        return result.ResultCode switch
        {
            CreateGrantResultCode.Success => (result.CreatedId, null),
            CreateGrantResultCode.UnauthorizedError => (
                Guid.Empty,
                "You are not allowed to create grants."
            ),
            _ => (Guid.Empty, result.Message),
        };
    }

    private async Task<(Guid, string?)> UpdateAsync(
        long? quantity,
        DateTime fromUtc,
        DateTime toUtc
    )
    {
        var result = await GrantService.UpdateGrantAsync(
            new UpdateGrantRequest(
                AccountId,
                _grant!.GrantId,
                _unit,
                quantity,
                fromUtc,
                toUtc,
                _grant.Tags
            )
        );
        return result.ResultCode switch
        {
            ModifyResultCode.Success => (_grant.GrantId, null),
            ModifyResultCode.UnauthorizedError => (
                Guid.Empty,
                "You are not allowed to change grants."
            ),
            ModifyResultCode.NotFoundError => (Guid.Empty, "This grant no longer exists."),
            _ => (Guid.Empty, result.Message),
        };
    }
}
