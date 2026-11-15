using Corely.Billing.Accounts.Models;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Persistence;

public sealed class AccountDataDeletionTests : IDisposable
{
    private static readonly Guid Deleted = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Kept = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private readonly BillingTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task SeedAsync(Guid accountId)
    {
        var ledger = new LedgerDriver(_host, accountId);
        await ledger.SeedGrantAsync(quantity: 100);
        await ledger.ProcessAsync("job:a/step:1", 10);
        await ledger.ReserveAsync("job:b/step:1", quantity: 1);
    }

    private Task<int> RowsForAsync(Guid accountId) =>
        _host.QueryAsync(async db =>
            await db.Grants.CountAsync(g => g.AccountId == accountId)
            + await db.ConsumptionEvents.CountAsync(e => e.AccountId == accountId)
        );

    private static readonly string[] DeletedWithTheAccount =
    [
        "ConsumptionEventEntity",
        "GrantEntity",
        "GrantRequestEntity",
        "RegistrationEntity",
    ];

    private static readonly Dictionary<string, string> Exempt = new()
    {
        ["UsagePriceEntity"] = "Prices are platform wide: no account owns one.",
    };

    [Fact]
    public async Task DeleteAccountDataAsync_CoversEveryEntity_ForTheWholeModel()
    {
        var entities = await _host.QueryAsync(db =>
            Task.FromResult(db.Model.GetEntityTypes().Select(t => t.ClrType.Name).ToList())
        );

        var uncovered = entities
            .Except(DeletedWithTheAccount)
            .Except(Exempt.Keys)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            uncovered.Count == 0,
            "Delete these with the account in AccountDataProcessor, or exempt them with a reason: "
                + string.Join(", ", uncovered)
        );
    }

    [Fact]
    public async Task DeleteAccountDataAsync_RemovesGrantsAndEveryConsumptionRowIncludingOpenHolds_ForOneAccountOnly()
    {
        await SeedAsync(Deleted);
        await SeedAsync(Kept);
        var keptBefore = await RowsForAsync(Kept);

        var result = await _host.WithScopeAsync(services =>
            services.GetRequiredService<IAccountDataService>().DeleteAccountDataAsync(Deleted)
        );

        Assert.Equal(DeleteAccountDataResultCode.Success, result.ResultCode);
        Assert.Equal(0, await RowsForAsync(Deleted));
        Assert.Equal(keptBefore, await RowsForAsync(Kept));
    }
}
