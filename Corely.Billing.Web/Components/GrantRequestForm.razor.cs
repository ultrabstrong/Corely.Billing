using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.Components;

public partial class GrantRequestForm
{
    [Inject]
    private IGrantActionGate ActionGate { get; set; } = null!;

    [Inject]
    private IUsageVocabulary Vocabulary { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid AccountId { get; set; }

    [Parameter]
    public EventCallback<Guid> OnSent { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private UsageOperation _operation;
    private long? _quantity;
    private string? _note;
    private bool _sending;
    private string? _error;

    private string OperationValue
    {
        get => _operation.Value;
        set => _operation = UsageOperation.From(value);
    }

    private UsageOperationDefinition? Definition => Vocabulary.Definition(_operation);

    private string UnitPlural =>
        Definition?.Unit is { } unit ? $"{Vocabulary.DisplayName(unit)}s" : string.Empty;

    protected override void OnInitialized()
    {
        _operation = Vocabulary.Operations[0].Operation;
    }

    private async Task SendAsync()
    {
        var isFeature = Definition?.Kind == GrantKind.Feature;
        if (!isFeature && _quantity is not >= 1)
        {
            _error = "Ask for at least one.";
            return;
        }

        _sending = true;
        var result = await InOperationAsync(async services =>
            await services
                .GetRequiredService<IGrantRequestService>()
                .RequestGrantAsync(
                    new RequestGrantRequest(
                        AccountId,
                        _operation,
                        isFeature ? null : _quantity,
                        _note,
                        await services
                            .GetRequiredService<IBillingAccountAccessor>()
                            .GetUserIdAsync()
                    )
                )
        );
        _sending = false;
        if (result is null)
            return;

        _error = result.ResultCode switch
        {
            RequestGrantResultCode.Success => null,
            RequestGrantResultCode.AlreadyPendingError =>
                "This account already has a pending request for that.",
            RequestGrantResultCode.UnauthorizedError => "You are not allowed to request grants.",
            _ => result.Message,
        };
        if (_error is null)
            await OnSent.InvokeAsync(result.CreatedId);
    }
}
