using Corely.Billing.GrantRequests.Entities;
using Corely.Billing.GrantRequests.Enums;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Common.Filtering;
using Corely.Common.Filtering.Filters;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Persistence;

public sealed class GrantRequestTests : IDisposable
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime From = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly BillingTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private Task<T> WithRequestsAsync<T>(Func<IGrantRequestService, Task<T>> work) =>
        _host.WithScopeAsync(services => work(services.GetRequiredService<IGrantRequestService>()));

    private async Task<Guid> PendingAsync(long quantity = 5_000) =>
        (
            await WithRequestsAsync(s =>
                s.RequestGrantAsync(
                    new RequestGrantRequest(
                        AccountId,
                        TestUsage.Extraction,
                        quantity,
                        "Month end batch"
                    )
                )
            )
        ).CreatedId;

    private static ApproveGrantRequestRequest Approval(Guid requestId) =>
        new(AccountId, requestId, From, From.AddYears(1));

    private Task<int> GrantCountAsync() => _host.QueryAsync(db => db.Grants.CountAsync());

    [Fact]
    public async Task RequestGrantAsync_RoundTripsThroughTheTable_ForARequest()
    {
        var requestId = await PendingAsync();

        var result = await WithRequestsAsync(s => s.GetGrantRequestAsync(AccountId, requestId));

        Assert.Equal(RetrieveResultCode.Success, result.ResultCode);
        Assert.Equal(GrantRequestStatus.Pending, result.Item!.Status);
        Assert.Equal(5_000, result.Item.Quantity);
        Assert.Equal("Month end batch", result.Item.Note);
        Assert.Equal(TestUsage.Page, result.Item.Unit);
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_LeavesOneGrantAndAnApprovedRequest_ForTwoApprovals()
    {
        var requestId = await PendingAsync();

        var first = await WithRequestsAsync(s => s.ApproveGrantRequestAsync(Approval(requestId)));
        var second = await WithRequestsAsync(s => s.ApproveGrantRequestAsync(Approval(requestId)));

        Assert.Equal(DecideGrantRequestResultCode.Success, first.ResultCode);
        Assert.Equal(DecideGrantRequestResultCode.AlreadyDecidedError, second.ResultCode);
        Assert.Equal(1, await GrantCountAsync());
        var request = await WithRequestsAsync(s => s.GetGrantRequestAsync(AccountId, requestId));
        Assert.Equal(GrantRequestStatus.Approved, request.Item!.Status);
        Assert.Equal(first.GrantId, request.Item.GrantId);
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_RollsBackItsGrant_ForARequestDecidedAfterItWasRead()
    {
        var requestId = await PendingAsync();

        var late = await _host.WithScopeAsync(async services =>
        {
            await services
                .GetRequiredService<IRepo<GrantRequestEntity>>()
                .GetAsync(e => e.GrantRequestId == requestId);

            await WithRequestsAsync(s => s.ApproveGrantRequestAsync(Approval(requestId)));

            return await services
                .GetRequiredService<IGrantRequestService>()
                .ApproveGrantRequestAsync(Approval(requestId));
        });

        Assert.Equal(DecideGrantRequestResultCode.AlreadyDecidedError, late.ResultCode);
        Assert.Equal(1, await GrantCountAsync());
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_WritesNothing_ForAnInvalidWindow()
    {
        var requestId = await PendingAsync();

        var result = await WithRequestsAsync(s =>
            s.ApproveGrantRequestAsync(Approval(requestId) with { ValidToUtc = From })
        );

        Assert.Equal(DecideGrantRequestResultCode.ValidationError, result.ResultCode);
        Assert.Equal(0, await GrantCountAsync());
        var request = await WithRequestsAsync(s => s.GetGrantRequestAsync(AccountId, requestId));
        Assert.Equal(GrantRequestStatus.Pending, request.Item!.Status);
    }

    [Fact]
    public async Task ListGrantRequestsAsync_FiltersByStatus_ForPendingOnly()
    {
        var declined = await PendingAsync();
        await WithRequestsAsync(s => s.DeclineGrantRequestAsync(new(AccountId, declined, "no")));
        var pending = await PendingAsync();

        var result = await WithRequestsAsync(s =>
            s.ListGrantRequestsAsync(
                new ListGrantRequestsRequest(
                    [AccountId],
                    Filter: Filter
                        .For<GrantRequest>()
                        .Where(
                            r => r.Status,
                            EnumFilter<GrantRequestStatus>.Equals(GrantRequestStatus.Pending)
                        )
                )
            )
        );

        Assert.Equal([pending], result.Data!.Items.Select(r => r.GrantRequestId));
    }
}
