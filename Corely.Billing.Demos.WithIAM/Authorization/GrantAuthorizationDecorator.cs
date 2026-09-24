using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Security.Constants;
using Corely.IAM.Security.Providers;

namespace Corely.Billing.Demos.WithIAM.Authorization;

internal sealed class GrantAuthorizationDecorator(
    IGrantService inner,
    IAuthorizationProvider authorization
) : IGrantService
{
    private const string DENIED = "Not authorized";

    public async Task<CreateGrantResult> CreateGrantAsync(
        CreateGrantRequest request,
        CancellationToken ct = default
    ) =>
        await AllowedAsync(AuthAction.Create)
            ? await inner.CreateGrantAsync(request, ct)
            : new CreateGrantResult(CreateGrantResultCode.UnauthorizedError, DENIED, Guid.Empty);

    public async Task<RetrieveSingleResult<Grant>> GetGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        await AllowedAsync(AuthAction.Read)
            ? await inner.GetGrantAsync(accountId, grantId, ct)
            : new RetrieveSingleResult<Grant>(RetrieveResultCode.UnauthorizedError, DENIED, null);

    public async Task<RetrieveListResult<Grant>> ListGrantsAsync(
        ListGrantsRequest request,
        CancellationToken ct = default
    ) =>
        await AllowedAsync(AuthAction.Read)
            ? await inner.ListGrantsAsync(request, ct)
            : new RetrieveListResult<Grant>(RetrieveResultCode.UnauthorizedError, DENIED, null);

    public async Task<ModifyResult> UpdateGrantAsync(
        UpdateGrantRequest request,
        CancellationToken ct = default
    ) =>
        await AllowedAsync(AuthAction.Update)
            ? await inner.UpdateGrantAsync(request, ct)
            : new ModifyResult(ModifyResultCode.UnauthorizedError, DENIED);

    public async Task<DeleteGrantResult> DeleteGrantAsync(
        Guid accountId,
        Guid grantId,
        CancellationToken ct = default
    ) =>
        await AllowedAsync(AuthAction.Delete)
            ? await inner.DeleteGrantAsync(accountId, grantId, ct)
            : new DeleteGrantResult(DeleteGrantResultCode.UnauthorizedError, DENIED);

    private Task<bool> AllowedAsync(AuthAction action) =>
        authorization.IsAuthorizedAsync(action, DemoUsage.GRANTS_RESOURCE);
}
