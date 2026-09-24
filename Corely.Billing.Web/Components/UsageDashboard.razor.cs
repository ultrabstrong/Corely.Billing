using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.Components;

public partial class UsageDashboard
{
    internal const string ALL = "all";
    internal const string CUSTOM = "custom";

    internal static readonly (string Key, string Label, int? Days)[] RangePresets =
    [
        ("7d", "7d", 7),
        ("30d", "30d", 30),
        ("90d", "90d", 90),
        ("1y", "1y", 365),
        (ALL, "All", null),
    ];

    [Inject]
    private IConsumptionService ConsumptionService { get; set; } = null!;

    [Inject]
    private IGrantService GrantService { get; set; } = null!;

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter]
    public string DefaultRange { get; set; } = "30d";

    private UsageChart? _chart;
    private ConsumptionTable? _table;
    private UsageFilter? _filter;
    private string? _error;
    private string _preset = "30d";
    private DateTime _from;
    private DateTime _to;
    private DateTime? _earliest;
    private Guid? _loadedFor;

    private IReadOnlySet<UsageUnit> _units = new HashSet<UsageUnit>();
    private IReadOnlySet<UsageOperation> _operations = new HashSet<UsageOperation>();
    private IReadOnlySet<string> _providers = new HashSet<string>();
    private IReadOnlySet<Guid> _grantIds = new HashSet<Guid>();

    private IReadOnlyList<(UsageUnit, string)> _unitOptions = [];
    private IReadOnlyList<(UsageOperation, string)> _operationOptions = [];
    private IReadOnlyList<(string, string)> _providerOptions = [];
    private IReadOnlyList<(Guid, string)> _grantOptions = [];

    private DateTime Today => TimeProvider.GetUtcNow().UtcDateTime.Date;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == AccountId)
            return;

        _loadedFor = AccountId;
        _unitOptions = [.. Vocabulary.Units.Select(u => (u.Unit, u.DisplayName))];
        _operationOptions = [.. Vocabulary.Operations.Select(o => (o.Operation, o.DisplayName))];
        _preset = DefaultRange;
        await SerializedAsync(LoadReferenceDataAsync);
        ApplyPreset();
    }

    public async Task RefreshAsync()
    {
        await SerializedAsync(LoadReferenceDataAsync);
        ApplyPreset();
        StateHasChanged();
        if (_chart is not null)
            await _chart.RefreshAsync();
        if (_table is not null)
            await _table.RefreshAsync();
    }

    private async Task LoadReferenceDataAsync()
    {
        var grants = await GrantService.ListGrantsAsync(
            new ListGrantsRequest(AccountId, Take: int.MaxValue)
        );
        if (grants.ResultCode == RetrieveResultCode.UnauthorizedError)
        {
            _error = BillingMessages.RetrieveError(grants.ResultCode, "view usage", grants.Message);
            return;
        }

        var grantList = grants.Data?.Items ?? [];
        _grantOptions = [.. grantList.Select(g => (g.GrantId, GrantOptionText(g)))];

        var providers = await ConsumptionService.ListProvidersAsync(AccountId);
        _providerOptions = [.. (providers.Item ?? []).Select(p => (p, p))];

        var earliest = await ConsumptionService.GetEarliestConsumptionAsync(AccountId);
        _earliest = new[]
        {
            earliest.Item,
            grantList.Count > 0 ? grantList.Min(g => g.ValidFromUtc) : null,
        }
            .Where(d => d is not null)
            .Min();
    }

    private string GrantOptionText(Grant grant) =>
        $"{UsageText.Count(grant.Quantity, Vocabulary.DisplayName(grant.Unit))}, {grant.ValidFromUtc:MMM d, yyyy} – {grant.ValidToUtc:MMM d, yyyy}";

    private void ApplyPreset()
    {
        if (_preset == CUSTOM)
        {
            BuildFilter();
            return;
        }

        var days = RangePresets.FirstOrDefault(p => p.Key == _preset).Days;
        _to = Today;
        _from = days is { } d ? Today.AddDays(-d) : (_earliest?.Date ?? Today.AddDays(-30));
        BuildFilter();
    }

    private void BuildFilter() =>
        _filter = new UsageFilter(
            DateTime.SpecifyKind(_from, DateTimeKind.Utc),
            DateTime.SpecifyKind(_to.AddDays(1).AddTicks(-1), DateTimeKind.Utc),
            NullIfEmpty(_units),
            NullIfEmpty(_operations),
            NullIfEmpty(_providers),
            NullIfEmpty(_grantIds)
        );

    private void SelectPreset(string preset)
    {
        _preset = preset;
        ApplyPreset();
    }

    private void FromChanged(ChangeEventArgs e)
    {
        if (!DateTime.TryParse(e.Value?.ToString(), out var from))
            return;
        _from = from.Date;
        _preset = CUSTOM;
        BuildFilter();
    }

    private void ToChanged(ChangeEventArgs e)
    {
        if (!DateTime.TryParse(e.Value?.ToString(), out var to))
            return;
        _to = to.Date;
        _preset = CUSTOM;
        BuildFilter();
    }

    private void UnitsChanged(IReadOnlySet<UsageUnit> units)
    {
        _units = units;
        BuildFilter();
    }

    private void OperationsChanged(IReadOnlySet<UsageOperation> operations)
    {
        _operations = operations;
        BuildFilter();
    }

    private void ProvidersChanged(IReadOnlySet<string> providers)
    {
        _providers = providers;
        BuildFilter();
    }

    private void GrantsChanged(IReadOnlySet<Guid> grantIds)
    {
        _grantIds = grantIds;
        BuildFilter();
    }

    private static IReadOnlyList<T>? NullIfEmpty<T>(IReadOnlySet<T> set) =>
        set.Count > 0 ? [.. set] : null;
}
