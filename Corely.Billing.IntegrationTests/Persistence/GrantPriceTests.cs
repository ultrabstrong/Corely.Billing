using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Prices.Models;
using Corely.Billing.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Persistence;

public sealed class GrantPriceTests : IDisposable
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly BillingTestHost _host = new(platformPrices: true);

    public void Dispose() => _host.Dispose();

    private DateTime Now => _host.TimeProvider.GetUtcNow().UtcDateTime;

    private Task<ChangeUsagePriceResult> PriceAsync(decimal amount) =>
        _host.WithScopeAsync(services =>
            services
                .GetRequiredService<IUsagePriceService>()
                .SetPriceAsync(new UsagePrice(TestUsage.Spend, Money.Usd(amount)))
        );

    private async Task<Grant> GrantAsync(decimal quantity)
    {
        var created = await _host.WithScopeAsync(services =>
            services
                .GetRequiredService<IGrantService>()
                .CreateGrantAsync(
                    new CreateGrantRequest(
                        AccountId,
                        TestUsage.Spend,
                        GrantKind.Consumable,
                        quantity,
                        Now.AddDays(-1),
                        Now.AddDays(30)
                    )
                )
        );
        Assert.Equal(CreateGrantResultCode.Success, created.ResultCode);
        var read = await _host.WithScopeAsync(services =>
            services.GetRequiredService<IGrantService>().GetGrantAsync(AccountId, created.CreatedId)
        );
        return read.Item!;
    }

    [Fact]
    public async Task Grant_KeepsTheValueItWasCreatedAt_ForAPriceChangedAfterwards()
    {
        await PriceAsync(0.001m);
        var before = await GrantAsync(2_000_000);

        await PriceAsync(0.002m);
        var after = await GrantAsync(2_000_000);
        var reread = await _host.WithScopeAsync(services =>
            services.GetRequiredService<IGrantService>().GetGrantAsync(AccountId, before.GrantId)
        );

        Assert.Equal(Money.Usd(2000), reread.Item!.Value());
        Assert.Equal(Money.Usd(4000), after.Value());
    }

    [Fact]
    public async Task Grant_IsUnpriced_ForAnOperationWithNoPrice()
    {
        var grant = await GrantAsync(100);

        Assert.Null(grant.UnitPrice);
        Assert.Null(grant.Value());
    }
}
