using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Prices.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Components;

public partial class GrantEditor
{
    [Inject]
    private IGrantActionGate ActionGate { get; set; } = null!;

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
    private decimal _quantity;
    private bool _unlimited;
    private bool _neverExpires;
    private DateTime _validFromUtc;
    private DateTime _validToUtc;
    private decimal? _unitPriceAmount;
    private string _currency = Money.USD;
    private Money? _currentPrice;
    private IReadOnlyDictionary<UsageOperation, Money?> _prices =
        new Dictionary<UsageOperation, Money?>();

    private Money? UnitPrice =>
        _unitPriceAmount is { } amount ? new Money(amount, _currency) : null;

    private bool IsNew => GrantId is null;

    private UsageOperationDefinition? Definition => Vocabulary.Definition(_operation);

    private GrantKind Kind => _grant?.Kind ?? Definition?.Kind ?? GrantKind.Consumable;

    private bool CanNeverExpire => Kind is GrantKind.Consumable or GrantKind.Capacity;

    private string QuantityLabel =>
        Kind switch
        {
            GrantKind.Capacity => "Capacity",
            GrantKind.Allowance => "Amount added",
            _ => "Quantity",
        };

    private string UnitSingular =>
        (_grant?.Unit ?? Definition?.Unit) is { } unit ? Vocabulary.DisplayName(unit) : "unit";

    private string UnitPlural =>
        (_grant?.Unit ?? Definition?.Unit) is { } unit
            ? $"{Vocabulary.DisplayName(unit)}s"
            : string.Empty;

    private string OperationValue
    {
        get => _operation.Value;
        set
        {
            _operation = UsageOperation.From(value);
            PriceFrom(_prices.GetValueOrDefault(_operation));
        }
    }

    private void PriceFrom(Money? price)
    {
        _currentPrice = price;
        _unitPriceAmount = price?.Amount;
        _currency = price?.Currency ?? Money.USD;
    }

    protected override Task OnParametersSetAsync() => InOperationAsync(LoadAsync);

    private async Task LoadAsync(IServiceProvider services)
    {
        _loading = true;
        _loadError = null;
        _error = null;
        _prices = (
            await services.GetRequiredService<IUsagePriceService>().ListPricesAsync()
        ).ByOperation();

        if (IsNew)
        {
            var today = TimeProvider.GetUtcNow().UtcDateTime.Date;
            _operation = Vocabulary.Operations[0].Operation;
            _quantity = 0;
            _unlimited = false;
            _neverExpires = false;
            _validFromUtc = today;
            _validToUtc = today.AddYears(1);
            PriceFrom(_prices.GetValueOrDefault(_operation));
            _loading = false;
            return;
        }

        var result = await services
            .GetRequiredService<IGrantService>()
            .GetGrantAsync(AccountId, GrantId!.Value);
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
        _unlimited = _grant.Quantity is null;
        _quantity = _grant.Quantity ?? 0;
        _validFromUtc = _grant.ValidFromUtc;
        _neverExpires = _grant.NeverExpires();
        _validToUtc = _neverExpires ? _validFromUtc.AddYears(1) : _grant.ValidToUtc;
        PriceFrom(_grant.UnitPrice);
        _currentPrice = _prices.GetValueOrDefault(_operation);
        _loading = false;
    }

    private async Task SaveAsync()
    {
        _error = Validate();
        if (_error is not null)
            return;

        _saving = true;
        decimal? quantity = Kind == GrantKind.Feature || _unlimited ? null : _quantity;
        var fromUtc = DateTime.SpecifyKind(_validFromUtc, DateTimeKind.Utc);
        var toUtc =
            CanNeverExpire && _neverExpires
                ? Grant.NEVER_EXPIRES_UTC
                : DateTime.SpecifyKind(_validToUtc, DateTimeKind.Utc);

        var saved = await InOperationAsync<(Guid, string?)?>(async services =>
        {
            var grants = services.GetRequiredService<IGrantService>();
            return IsNew
                ? await CreateAsync(grants, quantity, fromUtc, toUtc)
                : await UpdateAsync(grants, quantity, fromUtc, toUtc);
        });

        _saving = false;
        if (saved is not var (grantId, error))
            return;
        _error = error;
        if (error is null)
            await OnSaved.InvokeAsync(grantId);
    }

    private async Task ToggleDisabledAsync()
    {
        var grant = _grant!;
        _saving = true;
        var result = await InOperationAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .SetGrantDisabledAsync(
                    new SetGrantDisabledRequest(AccountId, grant.GrantId, !grant.Disabled)
                )
        );
        _saving = false;
        if (result is null)
            return;

        _error = result.ResultCode switch
        {
            ModifyResultCode.Success => null,
            ModifyResultCode.UnauthorizedError => "You are not allowed to change grants.",
            ModifyResultCode.NotFoundError => "This grant no longer exists.",
            _ => result.Message,
        };
        if (_error is null)
            grant.Disabled = !grant.Disabled;
    }

    private string? Validate() =>
        Kind != GrantKind.Feature && !_unlimited && _quantity < 0 ? "Quantity must be zero or more."
        : UnitPrice?.Problem() is { } priceProblem ? priceProblem
        : !(CanNeverExpire && _neverExpires) && _validToUtc <= _validFromUtc
            ? "Valid to must be after valid from."
        : null;

    private async Task<(Guid, string?)> CreateAsync(
        IGrantService grants,
        decimal? quantity,
        DateTime fromUtc,
        DateTime toUtc
    )
    {
        var result = await grants.CreateGrantAsync(
            new CreateGrantRequest(
                AccountId,
                _operation,
                Kind,
                quantity,
                fromUtc,
                toUtc,
                UnitPrice: Kind == GrantKind.Consumable ? UnitPrice : null
            )
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
        IGrantService grants,
        decimal? quantity,
        DateTime fromUtc,
        DateTime toUtc
    )
    {
        var result = await grants.UpdateGrantAsync(
            new UpdateGrantRequest(
                AccountId,
                _grant!.GrantId,
                quantity,
                fromUtc,
                toUtc,
                _grant.Tags,
                Kind == GrantKind.Consumable ? UnitPrice : null
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
