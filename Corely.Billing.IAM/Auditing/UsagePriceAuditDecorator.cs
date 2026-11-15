using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.IAM.Audits.Models;
using Corely.IAM.Audits.Providers;
using Corely.IAM.Security.Enums;

namespace Corely.Billing.IAM.Auditing;

internal sealed class UsagePriceAuditDecorator(
    IUsagePriceService inner,
    IAuditProvider auditProvider
) : IUsagePriceService
{
    private const string SERVICE = nameof(IUsagePriceService);

    public PriceSource Source => inner.Source;

    public Task<Money?> GetPriceAsync(UsageOperation operation, CancellationToken ct = default) =>
        inner.GetPriceAsync(operation, ct);

    public Task<IReadOnlyList<UsagePrice>> ListPricesAsync(CancellationToken ct = default) =>
        inner.ListPricesAsync(ct);

    public Task<ChangeUsagePriceResult> SetPriceAsync(
        UsagePrice price,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnPrice(AuthAction.Update),
            () => inner.SetPriceAsync(price, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    public Task<ChangeUsagePriceResult> ClearPriceAsync(
        UsageOperation operation,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            OnPrice(AuthAction.Delete),
            () => inner.ClearPriceAsync(operation, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );

    private static AuditCall OnPrice(AuthAction action) =>
        new(SERVICE, action, BillingResourceTypes.USAGE_PRICE_RESOURCE_TYPE);
}
