using Corely.Billing;
using Corely.Billing.Grants.DataAccess;
using Corely.Billing.Grants.Services;
using Corely.DataAccess.Interfaces.Repos;

namespace Corely.Billing.UnitTests.Grants.Services;

public class GrantReaderTests
{
    private static readonly Guid AccountId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AccountId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AccountId3 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task GetActiveGrantsAsync_Filters_By_Account_And_Time()
    {
        var repo = ServiceFixture.GetRequiredService<IRepo<GrantEntity>>();
        var reader = new GrantReader(repo);

        var now = DateTime.UtcNow;

        // Active for account 1
        await repo.CreateAsync(
            new GrantEntity
            {
                GrantId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Quantity = 100,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                ValidFromUtc = now.AddHours(-1),
                ValidToUtc = now.AddHours(1),
            }
        );
        // Expired for account 1
        await repo.CreateAsync(
            new GrantEntity
            {
                GrantId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Quantity = 50,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                ValidFromUtc = now.AddHours(-3),
                ValidToUtc = now.AddMinutes(-1),
            }
        );
        // Not yet valid for account 1
        await repo.CreateAsync(
            new GrantEntity
            {
                GrantId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Quantity = 25,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                ValidFromUtc = now.AddMinutes(1),
                ValidToUtc = now.AddHours(2),
            }
        );
        // Active for another account
        await repo.CreateAsync(
            new GrantEntity
            {
                GrantId = Guid.CreateVersion7(),
                AccountId = AccountId2,
                Quantity = 10,
                Unit = TestUsage.Page,
                Operation = TestUsage.Extraction,
                ValidFromUtc = now.AddHours(-1),
                ValidToUtc = now.AddHours(1),
            }
        );

        var results = await reader.GetActiveGrantsAsync(
            accountId: AccountId1,
            atUtc: now,
            operation: null,
            unit: null
        );

        Assert.Single(results.Grants!);
        Assert.Equal(AccountId1, results.Grants![0].AccountId);
        Assert.InRange(now, results.Grants[0].ValidFromUtc, results.Grants[0].ValidToUtc);
    }

    [Fact]
    public async Task GetActiveGrantsAsync_Filters_By_Unit_And_Operation_When_Specified()
    {
        var repo = ServiceFixture.GetRequiredService<IRepo<GrantEntity>>();
        var reader = new GrantReader(repo);

        var now = DateTime.UtcNow;

        // Active, matching unit + op
        var match = new GrantEntity
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = AccountId3,
            Quantity = 5,
            Unit = TestUsage.Document,
            Operation = TestUsage.NoOp,
            ValidFromUtc = now.AddMinutes(-5),
            ValidToUtc = now.AddMinutes(30),
        };
        await repo.CreateAsync(match);

        // Active, non-matching unit
        await repo.CreateAsync(
            new GrantEntity
            {
                GrantId = Guid.CreateVersion7(),
                AccountId = AccountId3,
                Quantity = 5,
                Unit = TestUsage.Page,
                Operation = TestUsage.NoOp,
                ValidFromUtc = now.AddMinutes(-5),
                ValidToUtc = now.AddMinutes(30),
            }
        );
        // Active, non-matching operation
        await repo.CreateAsync(
            new GrantEntity
            {
                GrantId = Guid.CreateVersion7(),
                AccountId = AccountId3,
                Quantity = 5,
                Unit = TestUsage.Document,
                Operation = TestUsage.Extraction,
                ValidFromUtc = now.AddMinutes(-5),
                ValidToUtc = now.AddMinutes(30),
            }
        );

        var results = await reader.GetActiveGrantsAsync(
            accountId: AccountId3,
            atUtc: now,
            operation: TestUsage.NoOp,
            unit: TestUsage.Document
        );

        Assert.Single(results.Grants!);
        var g = results.Grants![0];
        Assert.Equal(AccountId3, g.AccountId);
        Assert.Equal(TestUsage.NoOp, g.Operation);
        Assert.Equal(TestUsage.Document, g.Unit);
    }

    [Fact]
    public async Task GetActiveGrantsAsync_Maps_Tags_When_Present()
    {
        var repo = ServiceFixture.GetRequiredService<IRepo<GrantEntity>>();
        var reader = new GrantReader(repo);

        var now = DateTime.UtcNow;

        // Pre-serialized tags to ensure mapper deserializes correctly
        var tagsJson = "{\"contractId\":\"C-42\",\"tier\":\"gold\"}";

        await repo.CreateAsync(
            new GrantEntity
            {
                GrantId = Guid.CreateVersion7(),
                AccountId = AccountId1,
                Quantity = 1,
                Unit = TestUsage.Page,
                Operation = TestUsage.Other,
                ValidFromUtc = now.AddMinutes(-1),
                ValidToUtc = now.AddMinutes(10),
                TagsJson = tagsJson,
            }
        );

        var results = await reader.GetActiveGrantsAsync(
            accountId: AccountId1,
            atUtc: now,
            operation: null,
            unit: null
        );

        Assert.Single(results.Grants!);
        var grant = results.Grants![0];
        Assert.NotNull(grant.Tags);
        Assert.True(grant.Tags!.ContainsKey("contractId"));
        Assert.Equal("C-42", grant.Tags["contractId"]);
        Assert.Equal("gold", grant.Tags["tier"]);
    }
}
