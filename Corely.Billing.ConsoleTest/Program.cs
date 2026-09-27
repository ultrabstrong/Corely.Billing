using Corely.Billing;
using Corely.Billing.Grants.Models;
using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddBillingServices(
    BillingOptions
        .Create(new ConfigurationBuilder().Build())
        .RegisterOperation("text_generation", "Text Generation")
        .RegisterUnit("token", "token")
);
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var grants = scope.ServiceProvider.GetRequiredService<IGrantService>();
var quota = scope.ServiceProvider.GetRequiredService<IQuotaService>();
var consumption = scope.ServiceProvider.GetRequiredService<IConsumptionService>();
var accessor = scope.ServiceProvider.GetRequiredService<IOperationContextAccessor>();

var generation = UsageOperation.From("text_generation");
var token = UsageUnit.From("token");
var accountId = Guid.CreateVersion7();
var now = DateTime.UtcNow;

await grants.CreateGrantAsync(
    new CreateGrantRequest(accountId, generation, token, 100_000, now.AddDays(-1), now.AddDays(2))
);
await grants.CreateGrantAsync(
    new CreateGrantRequest(
        accountId,
        generation,
        token,
        1_000_000,
        now.AddDays(-1),
        now.AddDays(30)
    )
);

using (accessor.BeginScope(new OperationContext(Guid.CreateVersion7(), "batch:demo")))
{
    var reserved = await quota.ReserveAsync(
        new ReserveQuotaRequest(accountId, generation, token, 4_000, "text-model")
    );
    Console.WriteLine($"Reserve 4,000 tokens: {reserved.ResultCode}");

    var settled = await quota.SettleAsync(
        new SettleQuotaRequest(accountId, generation, token, 500_000)
    );
    Console.WriteLine(
        $"Settle at 500,000 tokens: {settled.ResultCode}, {settled.RemainingRatio:P0} of quota left"
    );
}

var list = await grants.ListGrantsAsync(new ListGrantsRequest(accountId));
var totals = await consumption.GetGrantConsumptionTotalsAsync(
    accountId,
    [.. list.Data!.Items.Select(g => g.GrantId)]
);

foreach (var grant in list.Data.Items.OrderBy(g => g.ValidToUtc))
{
    var used =
        totals.Item!.SingleOrDefault(t => t.GrantId == grant.GrantId)?.TotalConsumedQuantity ?? 0;
    Console.WriteLine(
        $"Grant of {grant.Quantity:N0}, expiring {grant.ValidToUtc:d}: {used:N0} used"
    );
}
