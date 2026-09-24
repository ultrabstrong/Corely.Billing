using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Corely.Billing.Web.Components;

public partial class UsageChart : IAsyncDisposable
{
    private const string MODULE_PATH =
        "./_content/Corely.Billing.Web/Components/UsageChart.razor.js";

    [Inject]
    private IConsumptionService ConsumptionService { get; set; } = null!;

    [Inject]
    private IGrantService GrantService { get; set; } = null!;

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter, EditorRequired]
    public UsageFilter Filter { get; set; } = null!;

    [Parameter]
    public string Title { get; set; } = "Used";

    private ElementReference _usageCanvas;
    private ElementReference _capacityCanvas;
    private IJSObjectReference? _module;
    private UsageChartModel? _model;
    private TimeBucket _bucket;
    private bool _loading = true;
    private bool _pendingDraw;
    private string? _error;
    private string? _loadedFor;

    private string Subtitle =>
        _bucket switch
        {
            TimeBucket.Week => "Per week",
            TimeBucket.Month => "Per month",
            _ => "Per day",
        };

    private bool ShowCharts => _model is { IsEmpty: false } && _error is null;

    private string UsageLabel =>
        _model is null
            ? Title
            : $"{Title}: {_model.Consumption.Sum():N0} used across {_model.Labels.Count} periods";

    protected override async Task OnParametersSetAsync()
    {
        var key = $"{AccountId}|{Filter.Signature}";
        if (_loadedFor == key)
            return;

        _loadedFor = key;
        await RefreshAsync();
    }

    public Task RefreshAsync() => SerializedAsync(LoadAsync);

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        _bucket = UsageChartModel.BucketFor(Filter.FromUtc, Filter.ToUtc);

        var series = await ConsumptionService.GetConsumptionTimeSeriesAsync(
            new GetConsumptionTimeSeriesRequest(
                AccountId,
                Filter.FromUtc,
                Filter.ToUtc,
                _bucket,
                Filter.Units,
                Filter.Operations,
                Filter.Providers,
                Filter.GrantIds
            )
        );
        if (series.ResultCode != RetrieveResultCode.Success)
        {
            _error = BillingMessages.RetrieveError(series.ResultCode, "view usage", series.Message);
            _loading = false;
            return;
        }

        var grants = await GrantService.ListGrantsAsync(
            new ListGrantsRequest(AccountId, Take: int.MaxValue)
        );
        List<Grant> grantList =
            grants.ResultCode == RetrieveResultCode.Success ? grants.Data?.Items ?? [] : [];

        _model = UsageChartModel.Build(series.Item ?? [], grantList, _bucket, Filter, Vocabulary);
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
                "renderUsage",
                _usageCanvas,
                _model.Labels,
                _model.Consumption
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
