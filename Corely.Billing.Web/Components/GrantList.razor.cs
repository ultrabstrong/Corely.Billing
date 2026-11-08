using Corely.Billing.GrantRequests.Enums;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Components;

public partial class GrantList
{
    [Inject]
    private IGrantActionGate ActionGate { get; set; } = null!;

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
    private List<GrantRequest> _history = [];
    private Guid? _userId;
    private (DialogMode Mode, GrantRequest Request)? _dialog;
    private string? _reason;
    private Guid? _loadedFor;
    private bool _exporting;
    private ExportOutcome? _export;

    private const int HISTORY_SIZE = 20;

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

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == AccountId)
            return;

        _loadedFor = AccountId;
        await RefreshAsync();
    }

    public Task RefreshAsync() => InOperationAsync(LoadAsync);

    private async Task ExportAsync()
    {
        _exporting = true;
        _export = await RunExportAsync(exporter => exporter.DownloadGrantsAsync(AccountId));
        _exporting = false;
    }

    private async Task LoadAsync(IServiceProvider services)
    {
        _loading = true;
        _error = null;

        var result = await services
            .GetRequiredService<IGrantService>()
            .ListGrantsAsync(new ListGrantsRequest([AccountId], Take: int.MaxValue));
        if (result.ResultCode != RetrieveResultCode.Success)
        {
            _error = result.ResultCode.ErrorMessage("view grants", result.Message);
            _grants = [];
            _requests = [];
            _loading = false;
            return;
        }

        _grants = result.Data?.Items ?? [];
        var requests = await RequestsAsync(services);
        _requests = [.. requests.Where(r => r.Status == GrantRequestStatus.Pending)];
        _history =
        [
            .. requests
                .Where(r => r.Status != GrantRequestStatus.Pending)
                .OrderByDescending(r => r.DecidedUtc ?? r.RequestedUtc)
                .Take(HISTORY_SIZE),
        ];
        _userId ??= await services.GetRequiredService<IBillingAccountAccessor>().GetUserIdAsync();
        _loading = false;
    }

    private async Task<List<GrantRequest>> RequestsAsync(IServiceProvider services)
    {
        var result = await services
            .GetRequiredService<IGrantRequestService>()
            .ListGrantRequestsAsync(new ListGrantRequestsRequest([AccountId], Take: int.MaxValue));
        return result.ResultCode == RetrieveResultCode.Success ? result.Data?.Items ?? [] : [];
    }

    private void ConfirmDelete(Grant grant) => _confirmingDelete = grant.GrantId;

    private void CancelDelete() => _confirmingDelete = null;

    private void Open(DialogMode mode, GrantRequest request)
    {
        _reason = null;
        _dialog = (mode, request);
    }

    private void CloseDialog()
    {
        if (!_deciding)
            _dialog = null;
    }

    private void OnDialogKey(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            CloseDialog();
    }

    // The reload after the decision reads in an operation of its own, so it sees what the decision wrote.
    private async Task ConfirmDialogAsync()
    {
        if (_dialog is not { } dialog)
            return;

        _deciding = true;
        var decided = await InOperationAsync(async services =>
        {
            var grantRequests = services.GetRequiredService<IGrantRequestService>();
            var userId = await services
                .GetRequiredService<IBillingAccountAccessor>()
                .GetUserIdAsync();
            var id = dialog.Request.GrantRequestId;
            var (code, message) = dialog.Mode switch
            {
                DialogMode.Approve => await ApproveAsync(grantRequests, id, userId),
                DialogMode.Decline => Outcome(
                    await grantRequests.DeclineGrantRequestAsync(
                        new DeclineGrantRequestRequest(AccountId, id, _reason, userId)
                    )
                ),
                _ => Outcome(
                    await grantRequests.CancelGrantRequestAsync(
                        new CancelGrantRequestRequest(AccountId, id, _reason, userId)
                    )
                ),
            };
            await DecidedAsync(code, message, dialog.Mode.Verb());
        });
        if (!decided)
            _deciding = false;
    }

    private async Task<(DecideGrantRequestResultCode, string)> ApproveAsync(
        IGrantRequestService grantRequests,
        Guid id,
        Guid? userId
    )
    {
        var today = Now.Date;
        var result = await grantRequests.ApproveGrantRequestAsync(
            new ApproveGrantRequestRequest(AccountId, id, today, today.AddYears(1), userId)
        );
        return (result.ResultCode, result.Message);
    }

    private static (DecideGrantRequestResultCode, string) Outcome(DeclineGrantRequestResult r) =>
        (r.ResultCode, r.Message);

    private static (DecideGrantRequestResultCode, string) Outcome(CancelGrantRequestResult r) =>
        (r.ResultCode, r.Message);

    private async Task DecidedAsync(
        DecideGrantRequestResultCode code,
        string message,
        string action
    )
    {
        _deciding = false;
        _dialog = null;
        if (
            code
            is DecideGrantRequestResultCode.Success
                or DecideGrantRequestResultCode.AlreadyDecidedError
        )
            await InOperationAsync(LoadAsync);
        _error = code.ErrorMessage(action, message);
    }

    private async Task DeleteAsync(Grant grant)
    {
        _deleting = true;
        var result = await InOperationAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .DeleteGrantAsync(grant.AccountId, grant.GrantId)
        );
        _deleting = false;
        _confirmingDelete = null;
        if (result is null)
            return;

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
