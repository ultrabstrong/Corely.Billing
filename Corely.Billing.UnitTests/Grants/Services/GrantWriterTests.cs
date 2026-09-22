using Corely.Billing;
using Corely.Billing.Grants.DataAccess;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Services;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Grants.Services;

public class GrantWriterTests
{
    private static readonly Guid TestAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task SaveAsync_Creates_When_Not_Exists()
    {
        var repo = ServiceFixture.GetRequiredService<IRepo<GrantEntity>>();
        var logger = ServiceFixture.GetRequiredService<ILogger<GrantWriter>>();

        var writer = new GrantWriter(repo, TestUsage.Vocabulary, logger);

        var ok = Grant.Create(
            accountId: TestAccountId,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            quantity: 10,
            validFromUtc: DateTime.UtcNow.AddDays(-1),
            validToUtc: DateTime.UtcNow.AddDays(1),
            grantId: Guid.CreateVersion7()
        );
        Assert.True(ok.IsSuccess);

        await writer.SaveAsync(ok.Value!);

        var entity = await repo.GetAsync(e => e.GrantId == ok.Value!.GrantId);
        Assert.NotNull(entity);
        Assert.Multiple(() =>
        {
            Assert.Equal(ok.Value!.AccountId, entity!.AccountId);
            Assert.Equal(ok.Value.Quantity, entity.Quantity);
            Assert.Equal(ok.Value.Unit, entity.Unit);
            Assert.Equal(ok.Value.Operation, entity.Operation);
            Assert.Equal(ok.Value.ValidFromUtc, entity.ValidFromUtc);
            Assert.Equal(ok.Value.ValidToUtc, entity.ValidToUtc);
            Assert.Equal(ok.Value.GrantId, entity.GrantId);
        });
    }

    [Fact]
    public async Task SaveAsync_Skips_When_Exists()
    {
        var repo = ServiceFixture.GetRequiredService<IRepo<GrantEntity>>();
        var logger = ServiceFixture.GetRequiredService<ILogger<GrantWriter>>();

        var writer = new GrantWriter(repo, TestUsage.Vocabulary, logger);

        var ok = Grant.Create(
            accountId: TestAccountId,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            quantity: 10,
            validFromUtc: DateTime.UtcNow.AddDays(-1),
            validToUtc: DateTime.UtcNow.AddDays(1),
            grantId: Guid.CreateVersion7()
        );
        Assert.True(ok.IsSuccess);

        await writer.SaveAsync(ok.Value!);

        var count = await repo.CountAsync(e => e.GrantId == ok.Value!.GrantId);
        Assert.Equal(1, count);

        await writer.SaveAsync(ok.Value!);

        count = await repo.CountAsync(e => e.GrantId == ok.Value!.GrantId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SaveAsync_WritesNothing_ForAnUnregisteredOperation()
    {
        var repo = ServiceFixture.GetRequiredService<IRepo<GrantEntity>>();
        var logger = ServiceFixture.GetRequiredService<ILogger<GrantWriter>>();

        var writer = new GrantWriter(repo, TestUsage.Vocabulary, logger);
        var grant = UnregisteredGrant(UsageOperation.From("never_registered"), TestUsage.Page);

        var result = await writer.SaveAsync(grant);

        Assert.Equal(SaveGrantResultCode.UnknownUsage, result.ResultCode);
        Assert.Equal(0, await repo.CountAsync(e => e.GrantId == grant.GrantId));
    }

    [Fact]
    public async Task SaveAsync_WritesNothing_ForAnUnregisteredUnit()
    {
        var repo = ServiceFixture.GetRequiredService<IRepo<GrantEntity>>();
        var logger = ServiceFixture.GetRequiredService<ILogger<GrantWriter>>();

        var writer = new GrantWriter(repo, TestUsage.Vocabulary, logger);
        var grant = UnregisteredGrant(TestUsage.Extraction, UsageUnit.From("never_registered"));

        var result = await writer.SaveAsync(grant);

        Assert.Equal(SaveGrantResultCode.UnknownUsage, result.ResultCode);
        Assert.Equal(0, await repo.CountAsync(e => e.GrantId == grant.GrantId));
    }

    [Fact]
    public async Task UpdateAsync_ChangesNothing_ForAnUnregisteredOperation()
    {
        var repo = ServiceFixture.GetRequiredService<IRepo<GrantEntity>>();
        var logger = ServiceFixture.GetRequiredService<ILogger<GrantWriter>>();

        var writer = new GrantWriter(repo, TestUsage.Vocabulary, logger);
        var grant = UnregisteredGrant(UsageOperation.From("never_registered"), TestUsage.Page);

        var result = await writer.UpdateAsync(grant);

        Assert.Equal(UpdateGrantResultCode.UnknownUsage, result.ResultCode);
    }

    private static Grant UnregisteredGrant(UsageOperation operation, UsageUnit unit)
    {
        var created = Grant.Create(
            accountId: TestAccountId,
            unit: unit,
            operation: operation,
            quantity: 10,
            validFromUtc: DateTime.UtcNow.AddDays(-1),
            validToUtc: DateTime.UtcNow.AddDays(1),
            grantId: Guid.CreateVersion7()
        );

        Assert.True(created.IsSuccess);
        return created.Value!;
    }
}
