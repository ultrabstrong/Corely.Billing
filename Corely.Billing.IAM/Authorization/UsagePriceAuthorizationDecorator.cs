using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Providers;

namespace Corely.Billing.IAM.Authorization;

internal sealed class UsagePriceAuthorizationDecorator(
    IUsagePriceService inner,
    IAuthorizationProvider authorizationProvider,
    IUserContextProvider userContextProvider
) : IUsagePriceService
{
    public PriceSource Source => inner.Source;

    public Task<Money?> GetPriceAsync(UsageOperation operation, CancellationToken ct = default) =>
        inner.GetPriceAsync(operation, ct);

    public Task<IReadOnlyList<UsagePrice>> ListPricesAsync(CancellationToken ct = default) =>
        inner.ListPricesAsync(ct);

    public async Task<ChangeUsagePriceResult> SetPriceAsync(
        UsagePrice price,
        CancellationToken ct = default
    ) =>
        await CanChangeAsync(AuthAction.Update)
            ? await inner.SetPriceAsync(price, ct)
            : Unauthorized();

    public async Task<ChangeUsagePriceResult> ClearPriceAsync(
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        await CanChangeAsync(AuthAction.Delete)
            ? await inner.ClearPriceAsync(operation, ct)
            : Unauthorized();

    private async Task<bool> CanChangeAsync(AuthAction action) =>
        userContextProvider.GetUserContext() is { } context
        && (
            context.IsSystemContext
            || (
                context.CurrentAccount?.IsPlatformAccount == true
                && await authorizationProvider.IsAuthorizedAsync(
                    action,
                    BillingResourceTypes.USAGE_PRICE_RESOURCE_TYPE
                )
            )
        );

    private static ChangeUsagePriceResult Unauthorized() =>
        new(
            ChangeUsagePriceResultCode.UnauthorizedError,
            "Only the platform owner changes prices, from the platform account"
        );
}
