using Corely.Billing.Accounts.Models;
using Corely.Billing.Accounts.Processors;
using Corely.Billing.Consumption.Entities;
using Corely.Billing.GrantRequests.Entities;
using Corely.Billing.Grants.Entities;
using Corely.Billing.Registrations.Entities;
using Corely.Billing.Services;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Logging.Abstractions;

namespace Corely.Billing.UnitTests.Accounts.Processors;

public class AccountDataProcessorTests
{
    private static readonly Guid Deleted = Guid.CreateVersion7();
    private static readonly Guid Kept = Guid.CreateVersion7();

    private readonly ServiceFactory _serviceFactory = new();

    private IRepo<T> Repo<T>()
        where T : class => _serviceFactory.GetRequiredService<IRepo<T>>();

    private async Task SeedAsync(Guid accountId)
    {
        var grantId = Guid.CreateVersion7();
        await Repo<GrantEntity>()
            .CreateAsync(
                new GrantEntity
                {
                    GrantId = grantId,
                    AccountId = accountId,
                    Operation = TestUsage.Extraction,
                }
            );
        await Repo<GrantRequestEntity>()
            .CreateAsync(
                new GrantRequestEntity
                {
                    GrantRequestId = Guid.CreateVersion7(),
                    AccountId = accountId,
                    Operation = TestUsage.Extraction,
                }
            );
        await Repo<RegistrationEntity>()
            .CreateAsync(
                new RegistrationEntity
                {
                    RegistrationId = Guid.CreateVersion7(),
                    AccountId = accountId,
                    GrantId = grantId,
                    Operation = TestUsage.Members,
                }
            );
        await Repo<ConsumptionEventEntity>()
            .CreateAsync(
                new ConsumptionEventEntity
                {
                    ConsumptionId = Guid.CreateVersion7(),
                    AccountId = accountId,
                    GrantId = grantId,
                    Operation = TestUsage.Extraction,
                    Unit = TestUsage.Page,
                    Provider = "provider",
                    IdempotencyKey = $"key-{accountId}",
                }
            );
    }

    private async Task<int> RowsForAsync(Guid accountId) =>
        (await Repo<GrantEntity>().ListAsync(g => g.AccountId == accountId)).Count
        + (await Repo<GrantRequestEntity>().ListAsync(r => r.AccountId == accountId)).Count
        + (await Repo<RegistrationEntity>().ListAsync(r => r.AccountId == accountId)).Count
        + (await Repo<ConsumptionEventEntity>().ListAsync(e => e.AccountId == accountId)).Count;

    [Fact]
    public async Task DeleteAccountDataAsync_DeletesEveryTable_ForTheAccount()
    {
        await SeedAsync(Deleted);

        var result = await _serviceFactory
            .GetRequiredService<IAccountDataService>()
            .DeleteAccountDataAsync(Deleted);

        Assert.Equal(DeleteAccountDataResultCode.Success, result.ResultCode);
        Assert.Equal(
            (1, 1, 1, 1),
            (result.ConsumptionEvents, result.Registrations, result.GrantRequests, result.Grants)
        );
        Assert.Equal(0, await RowsForAsync(Deleted));
    }

    [Fact]
    public async Task DeleteAccountDataAsync_LeavesEveryRowAlone_ForAnotherAccount()
    {
        await SeedAsync(Deleted);
        await SeedAsync(Kept);

        await _serviceFactory
            .GetRequiredService<IAccountDataService>()
            .DeleteAccountDataAsync(Deleted);

        Assert.Equal(4, await RowsForAsync(Kept));
    }

    [Fact]
    public async Task DeleteAccountDataAsync_SucceedsAndDeletesNothing_ForASecondRun()
    {
        await SeedAsync(Deleted);
        var service = _serviceFactory.GetRequiredService<IAccountDataService>();
        await service.DeleteAccountDataAsync(Deleted);

        var again = await service.DeleteAccountDataAsync(Deleted);

        Assert.Equal(DeleteAccountDataResultCode.Success, again.ResultCode);
        Assert.Equal(
            (0, 0, 0, 0),
            (again.ConsumptionEvents, again.Registrations, again.GrantRequests, again.Grants)
        );
    }

    [Fact]
    public async Task DeleteAccountDataAsync_DeletesGrantsLast_ForRowsThatReferenceThem()
    {
        var order = new List<string>();
        var consumption = Recording<ConsumptionEventEntity>(order, "consumption");
        var registrations = Recording<RegistrationEntity>(order, "registrations");
        var requests = Recording<GrantRequestEntity>(order, "requests");
        var grants = Recording<GrantEntity>(order, "grants");

        await new AccountDataProcessor(
            consumption.Object,
            registrations.Object,
            requests.Object,
            grants.Object,
            NullLogger<AccountDataProcessor>.Instance
        ).DeleteAccountDataAsync(Deleted);

        Assert.Equal(["consumption", "registrations", "requests", "grants"], order);
    }

    private static Mock<IRepo<T>> Recording<T>(List<string> order, string name)
        where T : class
    {
        var repo = new Mock<IRepo<T>>();
        repo.Setup(r =>
                r.ExecuteDeleteAsync(
                    It.IsAny<System.Linq.Expressions.Expression<Func<T, bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => order.Add(name))
            .ReturnsAsync(0);
        return repo;
    }
}
