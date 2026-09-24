using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.Components;

public partial class GrantList
{
    [Inject]
    private IGrantService GrantService { get; set; } = null!;

    [Inject]
    private IConsumptionService ConsumptionService { get; set; } = null!;

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter]
    public bool CanManage { get; set; }

    [Parameter]
    public string NewGrantHref { get; set; } = BillingWebRoutes.GRANT_NEW;

    [Parameter]
    public Func<Guid, string> GrantHref { get; set; } = BillingWebRoutes.GrantEditor;

    [Parameter]
    public EventCallback<Grant> OnDeleted { get; set; }

    private List<Grant> _grants = [];
    private Dictionary<Guid, long> _used = [];
    private bool _loading = true;
    private bool _deleting;
    private string? _error;
    private Guid? _confirmingDelete;
    private Guid? _loadedFor;

    private DateTime Now => TimeProvider.GetUtcNow().UtcDateTime;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == AccountId)
            return;

        _loadedFor = AccountId;
        await RefreshAsync();
    }

    public Task RefreshAsync() => SerializedAsync(LoadAsync);

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;

        var result = await GrantService.ListGrantsAsync(
            new ListGrantsRequest(AccountId, Take: int.MaxValue)
        );
        if (result.ResultCode != RetrieveResultCode.Success)
        {
            _error = BillingMessages.RetrieveError(
                result.ResultCode,
                "view grants",
                result.Message
            );
            _grants = [];
            _loading = false;
            return;
        }

        _grants = result.Data?.Items ?? [];
        _used = await LoadUsedAsync();
        _loading = false;
    }

    private async Task<Dictionary<Guid, long>> LoadUsedAsync()
    {
        if (_grants.Count == 0)
            return [];

        var totals = await ConsumptionService.GetGrantConsumptionTotalsAsync(
            AccountId,
            [.. _grants.Select(g => g.GrantId)]
        );
        return (totals.Item ?? []).ToDictionary(t => t.GrantId, t => t.TotalConsumedQuantity);
    }

    private GrantBalance BalanceOf(Grant grant) =>
        GrantBalance.For(grant, _used.GetValueOrDefault(grant.GrantId));

    private string WhenText(Grant grant, GrantStatus status) =>
        status switch
        {
            GrantStatus.Upcoming => $"Starts {BillingMessages.InDays(grant.ValidFromUtc - Now)}",
            GrantStatus.Active => $"Expires {BillingMessages.InDays(grant.ValidToUtc - Now)}",
            _ => $"Expired {BillingMessages.DaysAgo(Now - grant.ValidToUtc)}",
        };

    private void ConfirmDelete(Grant grant) => _confirmingDelete = grant.GrantId;

    private void CancelDelete() => _confirmingDelete = null;

    private Task DeleteAsync(Grant grant) => SerializedAsync(() => DeleteCoreAsync(grant));

    private async Task DeleteCoreAsync(Grant grant)
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
