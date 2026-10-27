using Corely.Billing.GrantRequests.Enums;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Enums;
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

    private IGrantRequestService GrantRequestService =>
        ScopedServices.GetRequiredService<IGrantRequestService>();

    private IBillingAccountAccessor AccountAccessor =>
        ScopedServices.GetRequiredService<IBillingAccountAccessor>();

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Inject]
    private TimeProvider TimeProvider { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter, EditorRequired]
    public string NewGrantHref { get; set; } = null!;

    [Parameter, EditorRequired]
    public string RequestGrantHref { get; set; } = null!;

    [Parameter, EditorRequired]
    public Func<Guid, string> GrantHref { get; set; } = null!;

    [Parameter]
    public EventCallback<Grant> OnDeleted { get; set; }

    [Parameter]
    public bool ShowExport { get; set; } = true;

    private List<Grant> _grants = [];
    private List<GrantRequest> _requests = [];
    private bool _loading = true;
    private bool _deleting;
    private bool _deciding;
    private string? _error;
    private Guid? _confirmingDelete;
    private Guid? _approving;
    private Guid? _declining;
    private string? _declineReason;
    private Guid? _loadedFor;
    private bool _exporting;
    private ExportOutcome? _export;

    private DateTime Now => TimeProvider.GetUtcNow().UtcDateTime;

    private bool CanRequestMore => Vocabulary.Unrequested(_requests).Count > 0;

    private IEnumerable<(GrantKind Kind, List<GrantRequest> Requests, List<Grant> Grants)> Groups =>
        Enum.GetValues<GrantKind>()
            .Select(kind =>
                (
                    kind,
                    _requests.Where(r => r.Kind == kind).ToList(),
                    _grants.Where(g => g.Kind == kind).ToList()
                )
            )
            .Where(group => group.Item2.Count > 0 || group.Item3.Count > 0);

    protected override async Task OnParametersSetSignedInAsync()
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
            new ListGrantsRequest([AccountId], Take: int.MaxValue)
        );
        if (result.ResultCode != RetrieveResultCode.Success)
        {
            _error = result.ResultCode.ErrorMessage("view grants", result.Message);
            _grants = [];
            _requests = [];
            _loading = false;
            return;
        }

        _grants = result.Data?.Items ?? [];
        _requests = await PendingRequestsAsync();
        _loading = false;
    }

    private async Task<List<GrantRequest>> PendingRequestsAsync()
    {
        var result = await GrantRequestService.ListGrantRequestsAsync(
            new ListGrantRequestsRequest(
                [AccountId],
                Status: GrantRequestStatus.Pending,
                Take: int.MaxValue
            )
        );
        return result.ResultCode == RetrieveResultCode.Success ? result.Data?.Items ?? [] : [];
    }

    private void ConfirmDelete(Grant grant) => _confirmingDelete = grant.GrantId;

    private void CancelDelete() => _confirmingDelete = null;

    private void ConfirmApprove(GrantRequest request)
    {
        _declining = null;
        _approving = request.GrantRequestId;
    }

    private void ConfirmDecline(GrantRequest request)
    {
        _approving = null;
        _declineReason = null;
        _declining = request.GrantRequestId;
    }

    private void CancelDecision()
    {
        _approving = null;
        _declining = null;
    }

    private async Task ApproveAsync(GrantRequest request)
    {
        var today = Now.Date;
        _deciding = true;
        var result = await GrantRequestService.ApproveGrantRequestAsync(
            new ApproveGrantRequestRequest(
                AccountId,
                request.GrantRequestId,
                today,
                today.AddYears(1),
                await AccountAccessor.GetUserIdAsync()
            )
        );
        await DecidedAsync(result.ResultCode, result.Message, "approve");
    }

    private async Task DeclineAsync(GrantRequest request)
    {
        _deciding = true;
        var result = await GrantRequestService.DeclineGrantRequestAsync(
            new DeclineGrantRequestRequest(
                AccountId,
                request.GrantRequestId,
                _declineReason,
                await AccountAccessor.GetUserIdAsync()
            )
        );
        await DecidedAsync(result.ResultCode, result.Message, "decline");
    }

    private async Task DecidedAsync(
        DecideGrantRequestResultCode code,
        string message,
        string action
    )
    {
        _deciding = false;
        CancelDecision();
        if (
            code
            is DecideGrantRequestResultCode.Success
                or DecideGrantRequestResultCode.AlreadyDecidedError
        )
            await LoadAsync();
        _error = code.ErrorMessage(action, message);
    }

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
