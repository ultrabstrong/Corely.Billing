using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Corely.Billing.Web.Components;

public partial class UsageChart : IAsyncDisposable
{
    private const string MODULE_PATH =
        "./_content/Corely.Billing.Web/Components/UsageChart.razor.js";

    internal static readonly UsageChartView[] Views = Enum.GetValues<UsageChartView>();
    internal static readonly UsageBreakdown[] Breakdowns = Enum.GetValues<UsageBreakdown>();

    [Inject]
    private IConsumptionService ConsumptionService { get; set; } = null!;

    [Inject]
    private IGrantService GrantService { get; set; } = null!;

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Inject]
    private UsageExporter Exporter { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter, EditorRequired]
    public UsageFilter Filter { get; set; } = null!;

    [Parameter]
    public string Title { get; set; } = "Used";

    [Parameter]
    public UsageChartView View { get; set; } = UsageChartView.Used;

    [Parameter]
    public UsageBreakdown BreakDownBy { get; set; } = UsageBreakdown.Operation;

    [Parameter]
    public bool ShowControls { get; set; } = true;

    [Parameter]
    public bool ShowExport { get; set; } = true;

    private ElementReference _usageCanvas;
    private ElementReference _capacityCanvas;
    private IJSObjectReference? _module;
    private UsageChartModel? _model;
    private TimeBucket _bucket;
    private UsageChartView _view;
    private UsageBreakdown _by;
    private UsageUnit? _unit;
    private IReadOnlyList<UsageUnit> _units = [];
    private bool _loading = true;
    private bool _pendingDraw;
    private bool _exporting;
    private string? _error;
    private ExportOutcome? _export;
    private string? _loadedFor;
    private (UsageChartView, UsageBreakdown)? _parametersFor;

    internal UsageChartModel? Model => _model;

    internal string Caption => _view == UsageChartView.Used ? Title : _view.Caption(_by);

    private string Subtitle =>
        _view == UsageChartView.Share
            ? "Whole range"
            : _bucket switch
            {
                TimeBucket.Week => "Per week",
                TimeBucket.Month => "Per month",
                _ => "Per day",
            };

    private bool ShowCharts => _model is { IsEmpty: false } && _error is null;

    private string ChartLabel =>
        _model is null
            ? Caption
            : $"{Caption}: {UsageText.Count(_model.Total, _model.UnitName)} across {_model.Labels.Count} periods";

    private string EmptyText =>
        _view == UsageChartView.Remaining
            ? "No limited grant covers this range, so there is no balance to draw."
            : "Nothing was used in this range, and no limited grant covers it.";

    protected override async Task OnParametersSetAsync()
    {
        if (_parametersFor != (View, BreakDownBy))
        {
            _parametersFor = (View, BreakDownBy);
            _view = View;
            _by = BreakDownBy;
            _loadedFor = null;
        }

        var key = $"{AccountId}|{Filter.Signature}";
        if (_loadedFor == key)
            return;

        _loadedFor = key;
        await RefreshAsync();
    }

    public Task RefreshAsync() => SerializedAsync(LoadAsync);

    private async Task ViewChangedAsync(ChangeEventArgs e)
    {
        if (Enum.TryParse<UsageChartView>(e.Value?.ToString(), out var view))
        {
            _view = view;
            await RefreshAsync();
        }
    }

    private async Task BreakdownChangedAsync(ChangeEventArgs e)
    {
        if (Enum.TryParse<UsageBreakdown>(e.Value?.ToString(), out var by))
        {
            _by = by;
            await RefreshAsync();
        }
    }

    private async Task UnitChangedAsync(ChangeEventArgs e)
    {
        _unit = _units.FirstOrDefault(u => u.Value == e.Value?.ToString());
        await RefreshAsync();
    }

    private Task ExportAsync() =>
        SerializedAsync(async () =>
        {
            if (_model is null)
                return;
            _exporting = true;
            _export = await Exporter.DownloadChartAsync(_model);
            _exporting = false;
        });

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        _export = null;
        _bucket = UsageChartModel.BucketFor(Filter.FromUtc, Filter.ToUtc);

        var grantsResult = await GrantService.ListGrantsAsync(
            new ListGrantsRequest(AccountId, Take: int.MaxValue)
        );
        List<Grant> grants =
            grantsResult.ResultCode == RetrieveResultCode.Success
                ? grantsResult.Data?.Items ?? []
                : [];

        var byUnit = await SeriesAsync(Request(ConsumptionDimension.Unit, Filter.Units));
        if (byUnit is null)
            return;

        _units = UnitsIn(byUnit, grants);
        if (_unit is null || !_units.Contains(_unit.Value))
            _unit = _units.Count > 0 ? _units[0] : null;

        if (_unit is not { } unit)
        {
            Finish(UsageChartModel.Empty(_view));
            return;
        }

        var relevant = UsageChartModel.RelevantGrants(grants, Filter, unit);
        var series = await SeriesForViewAsync(unit, relevant);
        if (series is null)
            return;

        var byGrant = grants.ToDictionary(g => g.GrantId.ToString());
        Finish(
            UsageChartModel.Build(
                _view,
                series,
                relevant,
                _bucket,
                Filter,
                unit,
                Vocabulary.DisplayName(unit),
                key => KeyLabel(key, byGrant),
                grant => grant.AllowanceAndExpiry(Vocabulary)
            )
        );
    }

    private async Task<IReadOnlyList<ConsumptionSeries>?> SeriesForViewAsync(
        UsageUnit unit,
        List<Grant> relevant
    )
    {
        if (_view.BreaksDown())
            return await SeriesAsync(Request(_by.ToDimension(), [unit]));

        var limited = relevant.Where(g => g.Quantity is not null).ToList();
        if (_view == UsageChartView.Used || (_view == UsageChartView.BurnUp && limited.Count == 0))
            return await SeriesAsync(Request(null, [unit]));
        if (limited.Count == 0)
            return [];

        // An allowance is spent by every provider, so BurnUp and Remaining read the limited grants'
        // own charges; Remaining also reads from when each grant started, for its opening balance.
        var from =
            _view == UsageChartView.Remaining
                ? new[] { limited.Min(g => g.ValidFromUtc), Filter.FromUtc }.Min()
                : Filter.FromUtc;
        return await SeriesAsync(
            new GetConsumptionTimeSeriesRequest(
                AccountId,
                from,
                Filter.ToUtc,
                _bucket,
                ConsumptionDimension.Grant,
                [unit],
                GrantIds: [.. limited.Select(g => g.GrantId)]
            )
        );
    }

    private GetConsumptionTimeSeriesRequest Request(
        ConsumptionDimension? by,
        IReadOnlyList<UsageUnit>? units
    ) =>
        new(
            AccountId,
            Filter.FromUtc,
            Filter.ToUtc,
            _bucket,
            by,
            units,
            Filter.Operations,
            Filter.Providers,
            Filter.GrantIds
        );

    private async Task<IReadOnlyList<ConsumptionSeries>?> SeriesAsync(
        GetConsumptionTimeSeriesRequest request
    )
    {
        var result = await ConsumptionService.GetConsumptionTimeSeriesAsync(request);
        if (result.ResultCode == RetrieveResultCode.Success)
            return result.Item ?? [];

        _error = result.ResultCode.ErrorMessage("view usage", result.Message);
        _loading = false;
        return null;
    }

    // Units with usage in the range, or with a grant live in it, in vocabulary order. The chart
    // never adds two units together.
    private IReadOnlyList<UsageUnit> UnitsIn(
        IReadOnlyList<ConsumptionSeries> byUnit,
        List<Grant> grants
    )
    {
        var present = byUnit
            .Where(s => s.Total > 0)
            .Select(s => UsageUnit.From(s.Key!))
            .Concat(
                grants
                    .Where(g => g.ValidFromUtc <= Filter.ToUtc && g.ValidToUtc >= Filter.FromUtc)
                    .Where(g => Filter.Units is not { Count: > 0 } || Filter.Units.Contains(g.Unit))
                    .Select(g => g.Unit)
            )
            .ToHashSet();
        var order = Vocabulary.Units.Select(u => u.Unit).ToList();
        return
        [
            .. present
                .OrderBy(u => order.IndexOf(u) is var i && i >= 0 ? i : int.MaxValue)
                .ThenBy(u => u.Value, StringComparer.Ordinal),
        ];
    }

    private string KeyLabel(string key, Dictionary<string, Grant> grants)
    {
        return _by switch
        {
            UsageBreakdown.Operation => Vocabulary.DisplayName(UsageOperation.From(key)),
            UsageBreakdown.Grant => grants.TryGetValue(key, out var grant)
                ? grant.AllowanceAndExpiry(Vocabulary)
                : $"Grant {key[^8..]}",
            _ => key,
        };
    }

    private void Finish(UsageChartModel model)
    {
        _model = model;
        _loading = false;
        _pendingDraw = true;
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_pendingDraw || _model is null)
            return;

        _pendingDraw = false;
        try
        {
            _module ??= await JS.InvokeAsync<IJSObjectReference>("import", MODULE_PATH);
            await _module.InvokeVoidAsync("destroyChart", _usageCanvas);
            await _module.InvokeVoidAsync("destroyChart", _capacityCanvas);
            if (_model.IsEmpty)
                return;

            await _module.InvokeVoidAsync(
                "renderChart",
                _usageCanvas,
                new
                {
                    kind = _model.View.JsKind(),
                    labels = _model.Labels,
                    unit = _model.UnitName,
                    series = _model.Series.Select(s => new
                    {
                        label = s.Label,
                        data = s.Data,
                        other = s.IsOther,
                        overdrawn = s.Overdrawn,
                    }),
                }
            );
            if (_model.Capacity.Count > 0)
            {
                await _module.InvokeVoidAsync(
                    "renderCapacity",
                    _capacityCanvas,
                    _model.Labels,
                    _model.Capacity.Select(c => new
                    {
                        label = c.Label,
                        data = c.Data,
                        other = c.Label == UsageChartModel.OTHER_GRANTS,
                    })
                );
            }
        }
        catch (JSDisconnectedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync("destroyChart", _usageCanvas);
            await _module.InvokeVoidAsync("destroyChart", _capacityCanvas);
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }
}
