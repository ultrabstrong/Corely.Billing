using Corely.Billing.Consumption.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Corely.Common.Filtering.Ordering;
using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.Components;

public partial class ConsumptionTable
{
    private static readonly (ConsumptionEventSortField Field, string Label)[] Columns =
    [
        (ConsumptionEventSortField.UtcTimestamp, "When (UTC)"),
        (ConsumptionEventSortField.Quantity, "Used"),
        (ConsumptionEventSortField.Unit, "Unit"),
        (ConsumptionEventSortField.Operation, "Operation"),
        (ConsumptionEventSortField.Provider, "Provider"),
    ];

    [Inject]
    private IConsumptionService ConsumptionService { get; set; } = null!;

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter, EditorRequired]
    public UsageFilter Filter { get; set; } = null!;

    [Parameter]
    public int PageSize { get; set; } = 25;

    [Parameter]
    public string Title { get; set; } = "Usage events";

    private List<ConsumptionEvent> _items = [];
    private int _totalCount;
    private int _skip;
    private ConsumptionEventSortField _sortBy = ConsumptionEventSortField.UtcTimestamp;
    private SortDirection _direction = SortDirection.Descending;
    private bool _loading = true;
    private string? _error;
    private string? _loadedFor;

    protected override async Task OnParametersSetAsync()
    {
        var key = $"{AccountId}|{Filter.Signature}|{PageSize}";
        if (_loadedFor == key)
            return;

        _loadedFor = key;
        _skip = 0;
        await RefreshAsync();
    }

    public Task RefreshAsync() => SerializedAsync(LoadAsync);

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;

        var result = await ConsumptionService.ListConsumptionEventsAsync(
            new ListConsumptionEventsRequest(
                AccountId,
                Filter.FromUtc,
                Filter.ToUtc,
                Filter.Units,
                Filter.Operations,
                Filter.Providers,
                Filter.GrantIds,
                _sortBy,
                _direction,
                _skip,
                PageSize
            )
        );

        if (result.ResultCode != RetrieveResultCode.Success)
        {
            _error = result.ResultCode.ErrorMessage("view usage events", result.Message);
            _items = [];
            _totalCount = 0;
        }
        else
        {
            _items = result.Data?.Items ?? [];
            _totalCount = result.Data?.TotalCount ?? 0;
        }

        _loading = false;
        StateHasChanged();
    }

    private async Task SortAsync(ConsumptionEventSortField field)
    {
        _direction =
            _sortBy == field && _direction == SortDirection.Descending
                ? SortDirection.Ascending
                : SortDirection.Descending;
        _sortBy = field;
        _skip = 0;
        await RefreshAsync();
    }

    private async Task PageAsync(int step)
    {
        _skip = Math.Clamp(_skip + (step * PageSize), 0, Math.Max(0, _totalCount - 1));
        await RefreshAsync();
    }

    private string? AriaSort(ConsumptionEventSortField field) =>
        _sortBy != field ? null
        : _direction == SortDirection.Descending ? "descending"
        : "ascending";

    private string SortIcon(ConsumptionEventSortField field) =>
        _sortBy != field ? "bi-arrow-down-up cbw-sort-idle"
        : _direction == SortDirection.Descending ? "bi-sort-down"
        : "bi-sort-up";
}
