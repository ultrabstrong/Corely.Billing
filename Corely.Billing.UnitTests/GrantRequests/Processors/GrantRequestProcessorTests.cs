using Corely.Billing.GrantRequests.Entities;
using Corely.Billing.GrantRequests.Enums;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.GrantRequests.Processors;
using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Enums;
using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Models;
using Corely.Billing.Validators;
using Corely.DataAccess.Interfaces.Repos;
using Corely.DataAccess.Interfaces.UnitOfWork;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.GrantRequests.Processors;

public class GrantRequestProcessorTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DeciderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly ServiceFactory _serviceFactory = new();
    private readonly IRepo<GrantRequestEntity> _requestRepo;
    private readonly IRepo<GrantEntity> _grantRepo;
    private readonly GrantRequestProcessor _processor;

    public GrantRequestProcessorTests()
    {
        _requestRepo = _serviceFactory.GetRequiredService<IRepo<GrantRequestEntity>>();
        _grantRepo = _serviceFactory.GetRequiredService<IRepo<GrantEntity>>();
        _processor = new GrantRequestProcessor(
            _requestRepo,
            _serviceFactory.GetRequiredService<IGrantProcessor>(),
            TestUsage.Vocabulary,
            _serviceFactory.GetRequiredService<IUnitOfWorkProvider>(),
            _serviceFactory.GetRequiredService<IValidationProvider>(),
            _serviceFactory.TimeProvider,
            _serviceFactory.GetRequiredService<ILogger<GrantRequestProcessor>>()
        );
    }

    private static RequestGrantRequest Ask(long quantity = 5_000, string? note = null) =>
        new(AccountId, TestUsage.Extraction, quantity, note);

    private async Task<Guid> PendingAsync(long quantity = 5_000)
    {
        var result = await _processor.RequestGrantAsync(Ask(quantity));
        Assert.Equal(RequestGrantResultCode.Success, result.ResultCode);
        return result.CreatedId;
    }

    private static ApproveGrantRequestRequest Approval(Guid grantRequestId) =>
        new(AccountId, grantRequestId, Now.Date, Now.Date.AddYears(1), DeciderId);

    [Fact]
    public async Task RequestGrantAsync_StoresAPendingRequest_ForAValidRequest()
    {
        var result = await _processor.RequestGrantAsync(Ask(note: "  Month end batch  "));

        Assert.Equal(RequestGrantResultCode.Success, result.ResultCode);
        var entity = await _requestRepo.GetAsync(e => e.GrantRequestId == result.CreatedId);
        Assert.NotNull(entity);
        Assert.Equal(GrantRequestStatus.Pending, entity.Status);
        Assert.Equal(5_000, entity.Quantity);
        Assert.Equal("Month end batch", entity.Note);
        Assert.Equal(Now, entity.RequestedUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task RequestGrantAsync_WritesNothing_ForLessThanOne(long quantity)
    {
        var result = await _processor.RequestGrantAsync(Ask(quantity));

        Assert.Equal(RequestGrantResultCode.ValidationError, result.ResultCode);
        Assert.Equal(0, await _requestRepo.CountAsync());
    }

    [Fact]
    public async Task RequestGrantAsync_RefusesASecond_ForTheSameOperationPending()
    {
        await PendingAsync();

        var result = await _processor.RequestGrantAsync(Ask(10));

        Assert.Equal(RequestGrantResultCode.AlreadyPendingError, result.ResultCode);
        Assert.Equal(1, await _requestRepo.CountAsync());
    }

    [Fact]
    public async Task RequestGrantAsync_AcceptsASecond_ForAnotherOperation()
    {
        await PendingAsync();

        var result = await _processor.RequestGrantAsync(Ask() with { Operation = TestUsage.Other });

        Assert.Equal(RequestGrantResultCode.Success, result.ResultCode);
    }

    [Fact]
    public async Task RequestGrantAsync_StoresTheDeclaredKindWithNoQuantity_ForAFeature()
    {
        var result = await _processor.RequestGrantAsync(
            new RequestGrantRequest(AccountId, TestUsage.Sftp, null)
        );

        Assert.Equal(RequestGrantResultCode.Success, result.ResultCode);
        var entity = await _requestRepo.GetAsync(e => e.GrantRequestId == result.CreatedId);
        Assert.Equal(GrantKind.Feature, entity!.Kind);
        Assert.Null(entity.Unit);
        Assert.Null(entity.Quantity);
    }

    [Fact]
    public async Task RequestGrantAsync_WritesNothing_ForAFeatureWithAQuantity()
    {
        var result = await _processor.RequestGrantAsync(
            new RequestGrantRequest(AccountId, TestUsage.Sftp, 3)
        );

        Assert.Equal(RequestGrantResultCode.ValidationError, result.ResultCode);
        Assert.Equal(0, await _requestRepo.CountAsync());
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_CreatesAGrantOfTheDeclaredKind_ForAnAllowanceRequest()
    {
        var asked = await _processor.RequestGrantAsync(
            new RequestGrantRequest(AccountId, TestUsage.Retention, 60)
        );

        var result = await _processor.ApproveGrantRequestAsync(Approval(asked.CreatedId));

        Assert.Equal(DecideGrantRequestResultCode.Success, result.ResultCode);
        var grant = await _grantRepo.GetAsync(e => e.GrantId == result.GrantId);
        Assert.Equal(GrantKind.Allowance, grant!.Kind);
        Assert.Equal(TestUsage.Day, grant.Unit);
        Assert.Equal(60, grant.Quantity);
    }

    [Fact]
    public async Task RequestGrantAsync_AcceptsANewOne_ForAnAlreadyDecidedRequest()
    {
        var first = await PendingAsync();
        await _processor.DeclineGrantRequestAsync(new(AccountId, first));

        var result = await _processor.RequestGrantAsync(Ask());

        Assert.Equal(RequestGrantResultCode.Success, result.ResultCode);
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_CreatesTheGrantAndLinksIt_ForAPendingRequest()
    {
        var requestId = await PendingAsync();

        var result = await _processor.ApproveGrantRequestAsync(Approval(requestId));

        Assert.Equal(DecideGrantRequestResultCode.Success, result.ResultCode);
        var grant = await _grantRepo.GetAsync(e => e.GrantId == result.GrantId);
        Assert.NotNull(grant);
        Assert.Equal(5_000, grant.Quantity);
        Assert.Equal(TestUsage.Extraction, grant.Operation);
        Assert.Equal(TestUsage.Page, grant.Unit);
        Assert.Equal(Now.Date, grant.ValidFromUtc);
        Assert.Equal(Now.Date.AddYears(1), grant.ValidToUtc);

        var request = await _requestRepo.GetAsync(e => e.GrantRequestId == requestId);
        Assert.Equal(GrantRequestStatus.Approved, request!.Status);
        Assert.Equal(result.GrantId, request.GrantId);
        Assert.Equal(DeciderId, request.DecidedByUserId);
        Assert.Equal(Now, request.DecidedUtc);
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_CreatesNoSecondGrant_ForADecidedRequest()
    {
        var requestId = await PendingAsync();
        await _processor.ApproveGrantRequestAsync(Approval(requestId));

        var result = await _processor.ApproveGrantRequestAsync(Approval(requestId));

        Assert.Equal(DecideGrantRequestResultCode.AlreadyDecidedError, result.ResultCode);
        Assert.Equal(1, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_LeavesItPending_ForAnInvalidWindow()
    {
        var requestId = await PendingAsync();

        var result = await _processor.ApproveGrantRequestAsync(
            Approval(requestId) with
            {
                ValidToUtc = Now.Date.AddDays(-1),
            }
        );

        Assert.Equal(DecideGrantRequestResultCode.ValidationError, result.ResultCode);
        var request = await _requestRepo.GetAsync(e => e.GrantRequestId == requestId);
        Assert.Equal(GrantRequestStatus.Pending, request!.Status);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_ReturnsNotFound_ForAnotherAccountsRequest()
    {
        var requestId = await PendingAsync();

        var result = await _processor.ApproveGrantRequestAsync(
            Approval(requestId) with
            {
                AccountId = Guid.CreateVersion7(),
            }
        );

        Assert.Equal(DecideGrantRequestResultCode.NotFoundError, result.ResultCode);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task DeclineGrantRequestAsync_RecordsTheReason_ForAPendingRequest()
    {
        var requestId = await PendingAsync();

        var result = await _processor.DeclineGrantRequestAsync(
            new(AccountId, requestId, " Use the annual plan ", DeciderId)
        );

        Assert.Equal(DecideGrantRequestResultCode.Success, result.ResultCode);
        var request = await _requestRepo.GetAsync(e => e.GrantRequestId == requestId);
        Assert.Equal(GrantRequestStatus.Declined, request!.Status);
        Assert.Equal("Use the annual plan", request.DecisionReason);
        Assert.Equal(DeciderId, request.DecidedByUserId);
        Assert.Null(request.GrantId);
        Assert.Equal(0, await _grantRepo.CountAsync());
    }

    [Fact]
    public async Task DeclineGrantRequestAsync_ChangesNothing_ForAnApprovedRequest()
    {
        var requestId = await PendingAsync();
        await _processor.ApproveGrantRequestAsync(Approval(requestId));

        var result = await _processor.DeclineGrantRequestAsync(new(AccountId, requestId));

        Assert.Equal(DecideGrantRequestResultCode.AlreadyDecidedError, result.ResultCode);
        var request = await _requestRepo.GetAsync(e => e.GrantRequestId == requestId);
        Assert.Equal(GrantRequestStatus.Approved, request!.Status);
    }

    [Fact]
    public async Task DeclineGrantRequestAsync_RefusesIt_ForAReasonTooLong()
    {
        var requestId = await PendingAsync();

        var result = await _processor.DeclineGrantRequestAsync(
            new(AccountId, requestId, new string('x', 501))
        );

        Assert.Equal(DecideGrantRequestResultCode.ValidationError, result.ResultCode);
    }

    [Fact]
    public async Task ListGrantRequestsAsync_PutsPendingFirst_ForMixedStatuses()
    {
        var pending = await PendingAsync();
        _serviceFactory.TimeProvider.Advance(TimeSpan.FromDays(1));
        var declined = (
            await _processor.RequestGrantAsync(Ask() with { Operation = TestUsage.Other })
        ).CreatedId;
        await _processor.DeclineGrantRequestAsync(new(AccountId, declined));

        var result = await _processor.ListGrantRequestsAsync(new([AccountId]));

        Assert.Equal(RetrieveResultCode.Success, result.ResultCode);
        Assert.Equal([pending, declined], result.Data!.Items.Select(r => r.GrantRequestId));
    }

    [Fact]
    public async Task GetGrantRequestAsync_ReturnsNotFound_ForAnotherAccount()
    {
        var requestId = await PendingAsync();

        var result = await _processor.GetGrantRequestAsync(Guid.CreateVersion7(), requestId);

        Assert.Equal(RetrieveResultCode.NotFoundError, result.ResultCode);
    }
}
