using System.Globalization;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Components;

public partial class PriceSettings
{
    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Parameter]
    public bool Editable { get; set; }

    [Parameter]
    public EventCallback OnChanged { get; set; }

    private bool _loading = true;
    private bool _saving;
    private bool _failed;
    private string? _message;
    private PriceSource Source { get; set; }
    private IReadOnlyDictionary<UsageOperation, Money?> _prices =
        new Dictionary<UsageOperation, Money?>();
    private readonly Dictionary<UsageOperation, string?> _entered = [];

    private IEnumerable<UsageOperationDefinition> Consumables =>
        Vocabulary.Operations.Where(o => o.Kind == GrantKind.Consumable);

    protected override Task OnInitializedAsync() => InOperationAsync(LoadAsync);

    private async Task LoadAsync(IServiceProvider services)
    {
        var prices = services.GetRequiredService<IUsagePriceService>();
        Source = prices.Source;
        _prices = (await prices.ListPricesAsync()).ByOperation();
        _entered.Clear();
        _loading = false;
    }

    private string UnitName(UsageOperationDefinition definition) =>
        definition.Unit is { } unit ? Vocabulary.DisplayName(unit) : "unit";

    private string? Entered(UsageOperation operation) =>
        _entered.TryGetValue(operation, out var text)
            ? text
            : _prices
                .GetValueOrDefault(operation)
                ?.Amount.ToString("0.############################", CultureInfo.InvariantCulture);

    private void Enter(UsageOperation operation, object? value) =>
        _entered[operation] = value?.ToString();

    private async Task SaveAsync(UsageOperation operation)
    {
        if (
            !decimal.TryParse(
                Entered(operation),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount
            )
        )
        {
            Show(true, "Enter the price as a number of dollars, like 0.001.");
            return;
        }

        await ChangeAsync(
            services =>
                services
                    .GetRequiredService<IUsagePriceService>()
                    .SetPriceAsync(new UsagePrice(operation, Money.Usd(amount))),
            $"{Vocabulary.DisplayName(operation)} now costs {Money.Usd(amount).ToString()} each, for grants created from now on."
        );
    }

    private Task ClearAsync(UsageOperation operation) =>
        ChangeAsync(
            services =>
                services.GetRequiredService<IUsagePriceService>().ClearPriceAsync(operation),
            $"{Vocabulary.DisplayName(operation)} has no price now. Grants created from now on show no money."
        );

    private async Task ChangeAsync(
        Func<IServiceProvider, Task<ChangeUsagePriceResult>> change,
        string done
    )
    {
        _saving = true;
        var result = await InOperationAsync(async services =>
        {
            var changed = await change(services);
            if (changed.ResultCode == ChangeUsagePriceResultCode.Success)
                await LoadAsync(services);
            return changed;
        });
        _saving = false;
        if (result is null)
            return;
        if (result.ResultCode == ChangeUsagePriceResultCode.Success)
            await OnChanged.InvokeAsync();

        Show(
            result.ResultCode != ChangeUsagePriceResultCode.Success,
            result.ResultCode switch
            {
                ChangeUsagePriceResultCode.Success => done,
                ChangeUsagePriceResultCode.UnauthorizedError =>
                    "You are not allowed to change prices.",
                _ => result.Message,
            }
        );
    }

    private void Show(bool failed, string message)
    {
        _failed = failed;
        _message = message;
    }
}
