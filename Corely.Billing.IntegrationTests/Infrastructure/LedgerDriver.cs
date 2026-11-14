using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Infrastructure;

public sealed class LedgerDriver(
    IBillingTestHost host,
    Guid accountId,
    UsageOperation? operation = null,
    UsageUnit? unit = null
)
{
    private readonly UsageOperation _operation = operation ?? TestUsage.Extraction;
    private readonly UsageUnit _unit = unit ?? TestUsage.Page;

    public async Task<Guid> SeedGrantAsync(decimal? quantity, int expiresInDays = 30)
    {
        var now = host.TimeProvider.GetUtcNow().UtcDateTime;
        var result = await host.WithScopeAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .CreateGrantAsync(
                    new CreateGrantRequest(
                        accountId,
                        _operation,
                        GrantKind.Consumable,
                        quantity,
                        now.AddDays(-2),
                        now.AddDays(expiresInDays)
                    )
                )
        );
        Assert.Equal(CreateGrantResultCode.Success, result.ResultCode);
        return result.CreatedId;
    }

    public async Task SetDisabledAsync(Guid grantId, bool disabled)
    {
        var result = await host.WithScopeAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .SetGrantDisabledAsync(new SetGrantDisabledRequest(accountId, grantId, disabled))
        );
        Assert.Equal(Corely.Billing.Models.ModifyResultCode.Success, result.ResultCode);
    }

    public async Task ProcessAsync(string idempotencyScope, decimal quantity)
    {
        await ReserveAsync(idempotencyScope, quantity: 1);
        await SettleAsync(idempotencyScope, quantity);
    }

    public Task<ReserveQuotaResult> ReserveAsync(string idempotencyScope, decimal quantity) =>
        host.InOperationAsync(
            idempotencyScope,
            services =>
                services
                    .GetRequiredService<IQuotaService>()
                    .ReserveAsync(
                        new ReserveQuotaRequest(accountId, _operation, _unit, quantity, "prov")
                    )
        );

    public Task<SettleQuotaResult> SettleAsync(string idempotencyScope, decimal quantity) =>
        host.InOperationAsync(
            idempotencyScope,
            services =>
                services
                    .GetRequiredService<IQuotaService>()
                    .SettleAsync(new SettleQuotaRequest(accountId, _operation, _unit, quantity))
        );

    public Task<SettleQuotaResult> ReleaseAsync(string idempotencyScope) =>
        host.InOperationAsync(
            idempotencyScope,
            services =>
                services
                    .GetRequiredService<IQuotaService>()
                    .ReleaseAsync(new ReleaseQuotaRequest(accountId, _operation, _unit))
        );

    public async Task<decimal> BalanceAsync(Guid grantId)
    {
        var result = await host.WithScopeAsync(services =>
            services.GetRequiredService<IGrantService>().GetGrantAsync(accountId, grantId)
        );
        return result.Item?.Used ?? 0;
    }
}
