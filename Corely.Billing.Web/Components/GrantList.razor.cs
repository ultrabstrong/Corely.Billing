using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Components;

public partial class GrantList
{
    private IGrantActionGate ActionGate => ScopedServices.GetRequiredService<IGrantActionGate>();

    private IGrantService GrantService => ScopedServices.GetRequiredService<IGrantService>();

    private IConsumptionService ConsumptionService =>
        ScopedServices.GetRequiredService<IConsumptionService>();

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter]
    public string NewGrantHref { get; set; } = BillingWebRoutes.GRANT_NEW;

    [Parameter]
    public Func<Guid, string> GrantHref { get; set; } = BillingWebRoutes.GrantEditor;

    [Parameter]
    public EventCallback<Grant> OnDeleted { get; set; }

    [Parameter]
    public bool ShowExport { get; set; } = true;

    private List<Grant> _grants = [];
    private Dictionary<Guid, long>? _used;
    private bool _loading = true;
    private bool _deleting;
    private string? _error;
    private Guid? _confirmingDelete;
    private Guid? _loadedFor;
    private bool _exporting;
    private ExportOutcome? _export;

    private DateTime Now => TimeProvider.GetUtcNow().UtcDateTime;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == AccountId)
            return;

        _loadedFor = AccountId;
        await RefreshAsync();
    }

    public Task RefreshAsync() => LoadAsync();

    private async Task ExportAsync()
    {
        _exporting = true;
        _export = await Exporter.DownloadGrantsAsync(AccountId);
        _exporting = false;
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;

        var result = await GrantService.ListGrantsAsync(
            new ListGrantsRequest(AccountId, Take: int.MaxValue)
        );
        if (result.ResultCode != RetrieveResultCode.Success)
        {
            _error = result.ResultCode.ErrorMessage("view grants", result.Message);
            _grants = [];
            _loading = false;
            return;
        }

        _grants = result.Data?.Items ?? [];
        _used = await ConsumptionService.UsedByGrantAsync(AccountId, _grants);
        _loading = false;
    }

    private GrantBalance? BalanceOf(Grant grant) =>
        _used is null ? null : GrantBalance.For(grant, _used.GetValueOrDefault(grant.GrantId));

    private void ConfirmDelete(Grant grant) => _confirmingDelete = grant.GrantId;

    private void CancelDelete() => _confirmingDelete = null;

    private async Task DeleteAsync(Grant grant)
    {
        _deleting = true;
        var result = await GrantService.DeleteGrantAsync(grant.AccountId, grant.GrantId);
        _deleting = false;
        _confirmingDelete = null;

        switch (result.ResultCode)
        {
            case DeleteGrantResultCode.Success:
                _grants.Remove(grant);
                await OnDeleted.InvokeAsync(grant);
                break;
            case DeleteGrantResultCode.NotFoundError:
                _grants.Remove(grant);
                _error = "That grant was already deleted.";
                break;
            case DeleteGrantResultCode.UnauthorizedError:
                _error = "You are not allowed to delete grants.";
                break;
        }
    }
}
