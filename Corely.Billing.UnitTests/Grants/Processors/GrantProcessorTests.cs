using Corely.Billing.Consumption.Models;
using Corely.Billing.Consumption.Processors;
using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Models;
using Corely.Billing.Registrations.Processors;
using Corely.Billing.Usage;
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
    private readonly Mock<IConsumptionReportProcessor> _consumptionReport = new();
    private readonly Mock<IRegistrationProcessor> _registrations = new();
    private readonly GrantProcessor _grantProcessor;

    public GrantProcessorTests()
    {
        _grantRepo = _serviceFactory.GetRequiredService<IRepo<GrantEntity>>();
        _consumptionReport
            .Setup(p =>
                p.GetGrantConsumptionTotalsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        _registrations
            .Setup(p =>
                p.CountInUseAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        _grantProcessor = new GrantProcessor(
            _grantRepo,
            _consumptionReport.Object,
            _registrations.Object,
            TestUsage.Vocabulary,
            _serviceFactory.GetRequiredService<IValidationProvider>(),
            _serviceFactory.GetRequiredService<ILogger<GrantProcessor>>()
        );
    }

    private static CreateGrantRequest CreateRequest(
        long? quantity = 100,
        UsageOperation? operation = null,
        GrantKind kind = GrantKind.Consumable
    ) =>
        new(
            AccountId,
            operation ?? TestUsage.Extraction,
            kind,
            quantity,
            Now.AddDays(-1),
            Now.AddDays(30)
        );

    private async Task<Guid> CreateGrantAsync(
        Guid? accountId = null,
        DateTime? validFromUtc = null,
        DateTime? validToUtc = null,
        GrantKind kind = GrantKind.Consumable
    )
    {
        var entity = new GrantEntity
        {
            GrantId = Guid.CreateVersion7(),
            AccountId = accountId ?? AccountId,
            Operation = TestUsage.Extraction,
            Kind = kind,
            Unit = TestUsage.Page,
            Quantity = 100,
            ValidFromUtc = validFromUtc ?? Now.AddDays(-1),
            ValidToUtc = validToUtc ?? Now.AddDays(30),
        };
        await _grantRepo.CreateAsync(entity);
        return entity.GrantId;
    }

    [Fact]
    public async Task CreateGrantAsync_PersistsTheGrantWithTheDeclaredUnit_ForAValidRequest()
    {
        var result = await _grantProcessor.CreateGrantAsync(CreateRequest());

        Assert.Equal(CreateGrantResultCode.Success, result.ResultCode);
        var entity = await _grantRepo.GetAsync(e => e.GrantId == result.CreatedId);
        Assert.NotNull(entity);
        Assert.Equal(100, entity.Quantity);
        Assert.Equal(GrantKind.Consumable, entity.Kind);
        Assert.Equal(TestUsage.Page, entity.Unit);
    }

    [Fact]
    public async Task CreateGrantAsync_WritesNothing_ForAnInvalidRequest()
    {
        var result = await _grantProcessor.CreateGrantAsync(CreateRequest(quantity: -1));

        Assert.Equal(CreateGrantResultCode.ValidationError, result.ResultCode);
        Assert.Equal(Guid.Empty, result.CreatedId);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Theory]
    [InlineData(GrantKind.Allowance)]
    [InlineData(GrantKind.Capacity)]
    [InlineData(GrantKind.Feature)]
    public async Task CreateGrantAsync_RefusesAndWritesNothing_ForAKindTheOperationDoesNotDeclare(
        GrantKind kind
    )
    {
        var result = await _grantProcessor.CreateGrantAsync(CreateRequest(kind: kind));

        Assert.Equal(CreateGrantResultCode.DeclarationMismatchError, result.ResultCode);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task CreateGrantAsync_RefusesAndWritesNothing_ForAUnitTheOperationDoesNotDeclare()
    {
        var result = await _grantProcessor.CreateGrantAsync(
            CreateRequest() with
            {
                Unit = TestUsage.Day,
            }
        );

        Assert.Equal(CreateGrantResultCode.DeclarationMismatchError, result.ResultCode);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task CreateGrantAsync_RefusesAndWritesNothing_ForAFeatureGrantWithAQuantity()
    {
        var result = await _grantProcessor.CreateGrantAsync(
            CreateRequest(quantity: 1, operation: TestUsage.Sftp, kind: GrantKind.Feature)
        );

        Assert.Equal(CreateGrantResultCode.ValidationError, result.ResultCode);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task CreateGrantAsync_RefusesAndWritesNothing_ForAFeatureGrantWithAUnit()
    {
        var result = await _grantProcessor.CreateGrantAsync(
            CreateRequest(quantity: null, operation: TestUsage.Sftp, kind: GrantKind.Feature) with
            {
                Unit = TestUsage.Day,
            }
        );

        Assert.Equal(CreateGrantResultCode.DeclarationMismatchError, result.ResultCode);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task CreateGrantAsync_PersistsAFeatureWithNoUnit_ForAFeatureOperation()
    {
        var result = await _grantProcessor.CreateGrantAsync(
            CreateRequest(quantity: null, operation: TestUsage.Sftp, kind: GrantKind.Feature)
        );

        Assert.Equal(CreateGrantResultCode.Success, result.ResultCode);
        var entity = await _grantRepo.GetAsync(e => e.GrantId == result.CreatedId);
        Assert.Null(entity!.Unit);
        Assert.Null(entity.Quantity);
    }

    [Fact]
    public async Task CreateGrantAsync_WritesNothing_ForAnUnregisteredOperation()
    {
        var result = await _grantProcessor.CreateGrantAsync(
            CreateRequest(operation: TestUsage.Unregistered)
        );

        Assert.Equal(CreateGrantResultCode.ValidationError, result.ResultCode);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task UpdateGrantAsync_ChangesTheGrantButNotWhatItGrants_ForAValidRequest()
    {
        var grantId = await CreateGrantAsync();

        var result = await _grantProcessor.UpdateGrantAsync(
            new UpdateGrantRequest(
                AccountId,
                grantId,
                250,
                Now,
                Now.AddDays(60),
                new() { ["plan"] = "growth" }
            )
        );

        Assert.Equal(ModifyResultCode.Success, result.ResultCode);
        var entity = await _grantRepo.GetAsync(e => e.GrantId == grantId);
        Assert.Equal(250, entity!.Quantity);
        Assert.Equal(TestUsage.Page, entity.Unit);
        Assert.Equal(TestUsage.Extraction, entity.Operation);
        Assert.Equal(GrantKind.Consumable, entity.Kind);
        Assert.Contains("growth", entity.TagsJson);
    }

    [Fact]
    public async Task UpdateGrantAsync_ReturnsNotFound_ForAnotherAccountsGrant()
    {
        var grantId = await CreateGrantAsync(accountId: Guid.CreateVersion7());

        var result = await _grantProcessor.UpdateGrantAsync(
            new UpdateGrantRequest(AccountId, grantId, 1, Now, Now.AddDays(1))
        );

        Assert.Equal(ModifyResultCode.NotFoundError, result.ResultCode);
    }

    [Fact]
    public async Task UpdateGrantAsync_ChangesNothing_ForAnInvalidRequest()
    {
        var grantId = await CreateGrantAsync();

        var result = await _grantProcessor.UpdateGrantAsync(
            new UpdateGrantRequest(AccountId, grantId, -5, Now, Now.AddDays(1))
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
    public async Task GetGrantAsync_ReturnsTheGrantWithWhatIsLeft_ForAConsumableGrant()
    {
        var grantId = await CreateGrantAsync();
        _consumptionReport
            .Setup(p =>
                p.GetGrantConsumptionTotalsAsync(
                    AccountId,
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([new GrantTotalConsumptions(grantId, 30)]);

        var result = await _grantProcessor.GetGrantAsync(AccountId, grantId);

        Assert.Equal(RetrieveResultCode.Success, result.ResultCode);
        Assert.Equal(30, result.Item!.Used);
        Assert.Equal(70, result.Item.Remaining);
        Assert.Null(result.Item.InUse);
    }

    [Fact]
    public async Task GetGrantAsync_ReturnsWhatIsRegistered_ForACapacityGrant()
    {
        var grantId = await CreateGrantAsync(kind: GrantKind.Capacity);
        _registrations
            .Setup(p =>
                p.CountInUseAsync(
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, long> { [grantId] = 4 });

        var grant = (await _grantProcessor.GetGrantAsync(AccountId, grantId)).Item!;

        Assert.Equal(4, grant.InUse);
        Assert.Null(grant.Used);
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
            GrantKind.Consumable,
            Now
        );

        Assert.Equal(live, Assert.Single(grants).GrantId);
    }

    [Fact]
    public async Task SetGrantDisabledAsync_DisablesThenEnables_ForAnExistingGrant()
    {
        var grantId = await CreateGrantAsync();

        var disabled = await _grantProcessor.SetGrantDisabledAsync(new(AccountId, grantId, true));
        var afterDisable = (await _grantProcessor.GetGrantAsync(AccountId, grantId)).Item!;
        await _grantProcessor.SetGrantDisabledAsync(new(AccountId, grantId, false));
        var afterEnable = (await _grantProcessor.GetGrantAsync(AccountId, grantId)).Item!;

        Assert.Equal(ModifyResultCode.Success, disabled.ResultCode);
        Assert.True(afterDisable.Disabled);
        Assert.False(afterEnable.Disabled);
    }

    [Fact]
    public async Task SetGrantDisabledAsync_ReturnsNotFound_ForAnotherAccountsGrant()
    {
        var grantId = await CreateGrantAsync(accountId: Guid.CreateVersion7());

        var result = await _grantProcessor.SetGrantDisabledAsync(new(AccountId, grantId, true));

        Assert.Equal(ModifyResultCode.NotFoundError, result.ResultCode);
        var entity = await _grantRepo.GetAsync(e => e.GrantId == grantId);
        Assert.False(entity!.Disabled);
    }

    [Fact]
    public async Task UpdateGrantAsync_KeepsItDisabled_ForADisabledGrant()
    {
        var grantId = await CreateGrantAsync();
        await _grantProcessor.SetGrantDisabledAsync(new(AccountId, grantId, true));

        await _grantProcessor.UpdateGrantAsync(
            new UpdateGrantRequest(AccountId, grantId, 250, Now, Now.AddDays(60))
        );

        Assert.True((await _grantProcessor.GetGrantAsync(AccountId, grantId)).Item!.Disabled);
    }

    [Fact]
    public async Task ListActiveGrantsAsync_SkipsADisabledGrant_ForNoHolds()
    {
        var live = await CreateGrantAsync();
        var paused = await CreateGrantAsync();
        await _grantProcessor.SetGrantDisabledAsync(new(AccountId, paused, true));

        var grants = await _grantProcessor.ListActiveGrantsAsync(
            AccountId,
            TestUsage.Extraction,
            GrantKind.Consumable,
            Now
        );

        Assert.Equal(live, Assert.Single(grants).GrantId);
    }

    [Fact]
    public async Task ListActiveGrantsAsync_KeepsADisabledGrant_ForAHoldStillOnIt()
    {
        var paused = await CreateGrantAsync();
        await _grantProcessor.SetGrantDisabledAsync(new(AccountId, paused, true));

        var grants = await _grantProcessor.ListActiveGrantsAsync(
            AccountId,
            TestUsage.Extraction,
            GrantKind.Consumable,
            Now,
            [paused]
        );

        Assert.Equal(paused, Assert.Single(grants).GrantId);
    }

    [Fact]
    public async Task ListActiveGrantsAsync_ReturnsNothing_ForADifferentKind()
    {
        await CreateGrantAsync();

        var grants = await _grantProcessor.ListActiveGrantsAsync(
            AccountId,
            TestUsage.Extraction,
            GrantKind.Allowance,
            Now
        );

        Assert.Empty(grants);
    }
}
