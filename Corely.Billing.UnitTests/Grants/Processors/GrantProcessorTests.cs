using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Models;
using Corely.Billing.Validators;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Grants.Processors;

public class GrantProcessorTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly ServiceFactory _serviceFactory = new();
    private readonly IRepo<GrantEntity> _grantRepo;
    private readonly GrantProcessor _grantProcessor;

    public GrantProcessorTests()
    {
        _grantRepo = _serviceFactory.GetRequiredService<IRepo<GrantEntity>>();
        _grantProcessor = new GrantProcessor(
            _grantRepo,
            _serviceFactory.GetRequiredService<IValidationProvider>(),
            _serviceFactory.GetRequiredService<ILogger<GrantProcessor>>()
        );
    }

    private static CreateGrantRequest CreateRequest(long quantity = 100) =>
        new(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            quantity,
            Now.AddDays(-1),
            Now.AddDays(30)
        );

    private async Task<Guid> CreateGrantAsync(
        Guid? accountId = null,
        DateTime? validFromUtc = null,
        DateTime? validToUtc = null
    )
    {
        var entity = new GrantEntity
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = accountId ?? AccountId,
            Operation = TestUsage.Extraction,
            Unit = TestUsage.Page,
            Quantity = 100,
            ValidFromUtc = validFromUtc ?? Now.AddDays(-1),
            ValidToUtc = validToUtc ?? Now.AddDays(30),
        };
        await _grantRepo.CreateAsync(entity);
        return entity.GrantId;
    }

    [Fact]
    public async Task CreateGrantAsync_PersistsTheGrant_ForAValidRequest()
    {
        var result = await _grantProcessor.CreateGrantAsync(CreateRequest());

        Assert.Equal(CreateGrantResultCode.Success, result.ResultCode);
        var entity = await _grantRepo.GetAsync(e => e.GrantId == result.CreatedId);
        Assert.NotNull(entity);
        Assert.Equal(100, entity.Quantity);
    }

    [Fact]
    public async Task CreateGrantAsync_WritesNothing_ForAnInvalidRequest()
    {
        var result = await _grantProcessor.CreateGrantAsync(CreateRequest(quantity: -1));

        Assert.Equal(CreateGrantResultCode.ValidationError, result.ResultCode);
        Assert.Equal(Guid.Empty, result.CreatedId);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task CreateGrantAsync_WritesNothing_ForAnUnregisteredUnit()
    {
        var request = CreateRequest() with { Unit = TestUsage.UnregisteredUnit };

        var result = await _grantProcessor.CreateGrantAsync(request);

        Assert.Equal(CreateGrantResultCode.ValidationError, result.ResultCode);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task UpdateGrantAsync_ChangesTheGrant_ForAValidRequest()
    {
        var grantId = await CreateGrantAsync();

        var result = await _grantProcessor.UpdateGrantAsync(
            new UpdateGrantRequest(
                AccountId,
                grantId,
                TestUsage.Document,
                250,
                Now,
                Now.AddDays(60),
                new() { ["plan"] = "growth" }
            )
        );

        Assert.Equal(ModifyResultCode.Success, result.ResultCode);
        var entity = await _grantRepo.GetAsync(e => e.GrantId == grantId);
        Assert.Equal(250, entity!.Quantity);
        Assert.Equal(TestUsage.Document, entity.Unit);
        Assert.Equal(TestUsage.Extraction, entity.Operation);
        Assert.Contains("growth", entity.TagsJson);
    }

    [Fact]
    public async Task UpdateGrantAsync_ReturnsNotFound_ForAnotherAccountsGrant()
    {
        var grantId = await CreateGrantAsync(accountId: Guid.CreateVersion7());

        var result = await _grantProcessor.UpdateGrantAsync(
            new UpdateGrantRequest(AccountId, grantId, TestUsage.Page, 1, Now, Now.AddDays(1))
        );

        Assert.Equal(ModifyResultCode.NotFoundError, result.ResultCode);
    }

    [Fact]
    public async Task UpdateGrantAsync_ChangesNothing_ForAnInvalidRequest()
    {
        var grantId = await CreateGrantAsync();

        var result = await _grantProcessor.UpdateGrantAsync(
            new UpdateGrantRequest(AccountId, grantId, TestUsage.Page, -5, Now, Now.AddDays(1))
        );

        Assert.Equal(ModifyResultCode.ValidationError, result.ResultCode);
        var entity = await _grantRepo.GetAsync(e => e.GrantId == grantId);
        Assert.Equal(100, entity!.Quantity);
    }

    [Fact]
    public async Task DeleteGrantAsync_RemovesTheGrant_ForAnExistingGrant()
    {
        var grantId = await CreateGrantAsync();

        var result = await _grantProcessor.DeleteGrantAsync(AccountId, grantId);

        Assert.Equal(DeleteGrantResultCode.Success, result.ResultCode);
        Assert.False(await _grantRepo.AnyAsync(e => e.GrantId == grantId));
    }

    [Fact]
    public async Task DeleteGrantAsync_ReturnsNotFound_ForAMissingGrant()
    {
        var result = await _grantProcessor.DeleteGrantAsync(AccountId, Guid.CreateVersion7());

        Assert.Equal(DeleteGrantResultCode.NotFoundError, result.ResultCode);
    }

    [Fact]
    public async Task GetGrantAsync_ReturnsTheGrant_ForAnExistingGrant()
    {
        var grantId = await CreateGrantAsync();

        var result = await _grantProcessor.GetGrantAsync(AccountId, grantId);

        Assert.Equal(RetrieveResultCode.Success, result.ResultCode);
        Assert.Equal(grantId, result.Item!.GrantId);
    }

    [Fact]
    public async Task GetGrantAsync_ReturnsNotFound_ForAnotherAccountsGrant()
    {
        var grantId = await CreateGrantAsync(accountId: Guid.CreateVersion7());

        var result = await _grantProcessor.GetGrantAsync(AccountId, grantId);

        Assert.Equal(RetrieveResultCode.NotFoundError, result.ResultCode);
        Assert.Null(result.Item);
    }

    [Fact]
    public async Task ListActiveGrantsAsync_ReturnsOnlyLiveGrants_ForMixedValidityWindows()
    {
        var live = await CreateGrantAsync();
        await CreateGrantAsync(validFromUtc: Now.AddDays(-60), validToUtc: Now.AddDays(-30));
        await CreateGrantAsync(validFromUtc: Now.AddDays(10), validToUtc: Now.AddDays(40));
        await CreateGrantAsync(accountId: Guid.CreateVersion7());

        var grants = await _grantProcessor.ListActiveGrantsAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Page,
            Now
        );

        Assert.Equal(live, Assert.Single(grants).GrantId);
    }

    [Fact]
    public async Task ListActiveGrantsAsync_ReturnsNothing_ForADifferentUnit()
    {
        await CreateGrantAsync();

        var grants = await _grantProcessor.ListActiveGrantsAsync(
            AccountId,
            TestUsage.Extraction,
            TestUsage.Document,
            Now
        );

        Assert.Empty(grants);
    }
}
