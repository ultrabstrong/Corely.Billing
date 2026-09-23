using Corely.Billing.Grants.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Infrastructure;

public sealed class LedgerDriver(IBillingTestHost host, Guid accountId)
{
    public async Task<Guid> SeedGrantAsync(long quantity, int expiresInDays = 30)
    {
        var now = host.TimeProvider.GetUtcNow().UtcDateTime;
        var result = await host.WithScopeAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .CreateGrantAsync(
                    new CreateGrantRequest(
                        accountId,
                        TestUsage.Extraction,
                        TestUsage.Page,
                        quantity,
                        now.AddDays(-2),
                        now.AddDays(expiresInDays)
                    )
                )
        );
        Assert.Equal(CreateGrantResultCode.Success, result.ResultCode);
        return result.CreatedId;
    }

    public async Task ProcessAsync(string idempotencyScope, long quantity)
    {
        await ReserveAsync(idempotencyScope, quantity: 1);
        await SettleAsync(idempotencyScope, quantity);
    }

    public Task<ReserveQuotaResult> ReserveAsync(string idempotencyScope, long quantity) =>
        host.InOperationAsync(
            idempotencyScope,
            services =>
                services
                    .GetRequiredService<IQuotaService>()
                    .ReserveAsync(
                        new ReserveQuotaRequest(
                            accountId,
                            TestUsage.Extraction,
                            TestUsage.Page,
                            quantity,
                            "prov"
                        )
                    )
        );

    public Task<SettleQuotaResult> SettleAsync(string idempotencyScope, long quantity) =>
        host.InOperationAsync(
            idempotencyScope,
            services =>
                services
                    .GetRequiredService<IQuotaService>()
                    .SettleAsync(
                        new SettleQuotaRequest(
                            accountId,
                            TestUsage.Extraction,
                            TestUsage.Page,
                            quantity
                        )
                    )
        );

    public Task<SettleQuotaResult> ReleaseAsync(string idempotencyScope) =>
        host.InOperationAsync(
            idempotencyScope,
            services =>
                services
                    .GetRequiredService<IQuotaService>()
                    .ReleaseAsync(
                        new ReleaseQuotaRequest(accountId, TestUsage.Extraction, TestUsage.Page)
                    )
        );

    public async Task<long> BalanceAsync(Guid grantId)
    {
        var result = await host.WithScopeAsync(services =>
            services
                .GetRequiredService<IConsumptionService>()
                .GetGrantConsumptionTotalsAsync(accountId, [grantId])
        );
        return result.Item?.FirstOrDefault(t => t.GrantId == grantId)?.TotalConsumedQuantity ?? 0L;
    }
}
