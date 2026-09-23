using Corely.Billing;
using Corely.Billing.Grants.Models;
using Corely.Billing.Operations;
using Corely.Billing.Quota.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// No database: without an EF configuration factory, BillingOptions registers in-memory
// repositories. They live as long as the DI scope, so the whole demo runs in one.
var services = new ServiceCollection();
services.AddLogging();
services.AddBillingServices(
    BillingOptions
        .Create(new ConfigurationBuilder().Build())
        .RegisterOperation("document_extraction", "Document Extraction")
        .RegisterUnit("page", "page")
);
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var grants = scope.ServiceProvider.GetRequiredService<IGrantService>();
var quota = scope.ServiceProvider.GetRequiredService<IQuotaService>();
var consumption = scope.ServiceProvider.GetRequiredService<IConsumptionService>();
var accessor = scope.ServiceProvider.GetRequiredService<IOperationContextAccessor>();

var extraction = UsageOperation.From("document_extraction");
var page = UsageUnit.From("page");
var accountId = Guid.CreateVersion7();
var now = DateTime.UtcNow;

await grants.CreateGrantAsync(
    new CreateGrantRequest(accountId, extraction, page, 100, now.AddDays(-1), now.AddDays(2))
);
await grants.CreateGrantAsync(
    new CreateGrantRequest(accountId, extraction, page, 1000, now.AddDays(-1), now.AddDays(30))
);

using (accessor.BeginScope(new OperationContext(Guid.CreateVersion7(), "job:demo/step:extract")))
{
    var reserved = await quota.ReserveAsync(
        new ReserveQuotaRequest(accountId, extraction, page, 1, "demo")
    );
    Console.WriteLine($"Reserve 1 page: {reserved.ResultCode}");

    var settled = await quota.SettleAsync(new SettleQuotaRequest(accountId, extraction, page, 500));
    Console.WriteLine(
        $"Settle at 500 pages: {settled.ResultCode}, {settled.RemainingRatio:P0} of quota left"
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
    Console.WriteLine($"Grant of {grant.Quantity}, expiring {grant.ValidToUtc:d}: {used} used");
}
