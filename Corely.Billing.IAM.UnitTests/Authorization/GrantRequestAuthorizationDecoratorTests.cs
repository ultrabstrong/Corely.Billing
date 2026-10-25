using Corely.Billing.GrantRequests.Models;
using Corely.Billing.IAM.Authorization;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Models;
using Corely.IAM.Users.Providers;
using static Corely.Billing.IAM.UnitTests.TestUsage;

namespace Corely.Billing.IAM.UnitTests.Authorization;

public class GrantRequestAuthorizationDecoratorTests
{
    private const string GRANT = BillingResourceTypes.GRANT_RESOURCE_TYPE;
    private const string GRANT_REQUEST = BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE;

    private static readonly Guid RequestId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly Mock<IGrantRequestService> _inner = new();
    private readonly Mock<IAuthorizationProvider> _authorization = new();

    public GrantRequestAuthorizationDecoratorTests()
    {
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(true);
        _authorization
            .Setup(p =>
                p.IsAuthorizedAsync(It.IsAny<AuthAction>(), It.IsAny<string>(), It.IsAny<Guid[]>())
            )
            .ReturnsAsync(true);
        _inner
            .Setup(s =>
                s.ApproveGrantRequestAsync(
                    It.IsAny<ApproveGrantRequestRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApproveGrantRequestResult(DecideGrantRequestResultCode.Success, "", GrantId)
            );
        _inner
            .Setup(s =>
                s.DeclineGrantRequestAsync(
                    It.IsAny<DeclineGrantRequestRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new DeclineGrantRequestResult(DecideGrantRequestResultCode.Success, ""));
    }

    private UserContext? _userContext;

    private GrantRequestAuthorizationDecorator Decorator() =>
        new(
            _inner.Object,
            _authorization.Object,
            Mock.Of<IUserContextProvider>(p => p.GetUserContext() == _userContext),
            new AccountReadScope(
                _authorization.Object,
                Mock.Of<IUserContextProvider>(p => p.GetUserContext() == _userContext)
            )
        );

    private static readonly Guid SignedInUserId = Guid.Parse(
        "55555555-5555-5555-5555-555555555555"
    );

    private void SignIn() =>
        _userContext = new UserContext(
            new User { Id = SignedInUserId, Username = "priya" },
            null,
            "device",
            []
        );

    [Fact]
    public async Task ApproveGrantRequestAsync_RecordsTheSignedInUser_ForAClaimedDecider()
    {
        SignIn();

        await Decorator()
            .ApproveGrantRequestAsync(Approval with { DecidedByUserId = Guid.CreateVersion7() });

        _inner.Verify(s =>
            s.ApproveGrantRequestAsync(
                It.Is<ApproveGrantRequestRequest>(r => r.DecidedByUserId == SignedInUserId),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Fact]
    public async Task DeclineGrantRequestAsync_RecordsTheSignedInUser_ForAClaimedDecider()
    {
        SignIn();

        await Decorator()
            .DeclineGrantRequestAsync(Decline with { DecidedByUserId = Guid.CreateVersion7() });

        _inner.Verify(s =>
            s.DeclineGrantRequestAsync(
                It.Is<DeclineGrantRequestRequest>(r => r.DecidedByUserId == SignedInUserId),
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Fact]
    public async Task RequestGrantAsync_RecordsTheSignedInUser_ForAClaimedRequester()
    {
        SignIn();
        _inner
            .Setup(s =>
                s.RequestGrantAsync(It.IsAny<RequestGrantRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new RequestGrantResult(RequestGrantResultCode.Success, "", RequestId));

        await Decorator()
            .RequestGrantAsync(
                new(AccountId, Extraction, 10, RequestedByUserId: Guid.CreateVersion7())
            );

        _inner.Verify(s =>
            s.RequestGrantAsync(
                It.Is<RequestGrantRequest>(r => r.RequestedByUserId == SignedInUserId),
                It.IsAny<CancellationToken>()
            )
        );
    }

    private void Deny(AuthAction action, string resource) =>
        _authorization
            .Setup(p => p.IsAuthorizedAsync(action, resource, It.IsAny<Guid[]>()))
            .ReturnsAsync(false);

    private static ApproveGrantRequestRequest Approval =>
        new(AccountId, RequestId, DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

    private static DeclineGrantRequestRequest Decline => new(AccountId, RequestId);

    [Fact]
    public async Task RequestGrantAsync_ChecksCreateOnGrantRequest_ForARequest()
    {
        _inner
            .Setup(s =>
                s.RequestGrantAsync(It.IsAny<RequestGrantRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new RequestGrantResult(RequestGrantResultCode.Success, "", RequestId));

        var result = await Decorator().RequestGrantAsync(new(AccountId, Extraction, 10));

        Assert.Equal(RequestGrantResultCode.Success, result.ResultCode);
        _authorization.Verify(
            p => p.IsAuthorizedAsync(AuthAction.Create, GRANT_REQUEST),
            Times.Once
        );
    }

    [Fact]
    public async Task RequestGrantAsync_ReturnsUnauthorized_ForNoCreateOnGrantRequest()
    {
        Deny(AuthAction.Create, GRANT_REQUEST);

        var result = await Decorator().RequestGrantAsync(new(AccountId, Extraction, 10));

        Assert.Equal(RequestGrantResultCode.UnauthorizedError, result.ResultCode);
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_CallsInner_ForUpdateOnTheRequestAndCreateOnGrant()
    {
        var result = await Decorator().ApproveGrantRequestAsync(Approval);

        Assert.Equal(DecideGrantRequestResultCode.Success, result.ResultCode);
        _authorization.Verify(
            p => p.IsAuthorizedAsync(AuthAction.Update, GRANT_REQUEST, RequestId),
            Times.Once
        );
        _authorization.Verify(p => p.IsAuthorizedAsync(AuthAction.Create, GRANT), Times.Once);
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_ReturnsUnauthorized_ForUpdateWithoutCreateOnGrant()
    {
        Deny(AuthAction.Create, GRANT);

        var result = await Decorator().ApproveGrantRequestAsync(Approval);

        Assert.Equal(DecideGrantRequestResultCode.UnauthorizedError, result.ResultCode);
        _inner.Verify(
            s =>
                s.ApproveGrantRequestAsync(
                    It.IsAny<ApproveGrantRequestRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ApproveGrantRequestAsync_ReturnsUnauthorized_ForCreateOnGrantWithoutUpdate()
    {
        Deny(AuthAction.Update, GRANT_REQUEST);

        var result = await Decorator().ApproveGrantRequestAsync(Approval);

        Assert.Equal(DecideGrantRequestResultCode.UnauthorizedError, result.ResultCode);
    }

    [Fact]
    public async Task DeclineGrantRequestAsync_NeedsNoCreateOnGrant_ForUpdateOnTheRequest()
    {
        Deny(AuthAction.Create, GRANT);

        var result = await Decorator().DeclineGrantRequestAsync(Decline);

        Assert.Equal(DecideGrantRequestResultCode.Success, result.ResultCode);
    }

    [Fact]
    public async Task DeclineGrantRequestAsync_ReturnsUnauthorized_ForNoUpdateOnTheRequest()
    {
        Deny(AuthAction.Update, GRANT_REQUEST);

        var result = await Decorator().DeclineGrantRequestAsync(Decline);

        Assert.Equal(DecideGrantRequestResultCode.UnauthorizedError, result.ResultCode);
    }

    [Fact]
    public async Task DeclineGrantRequestAsync_ReturnsUnauthorized_ForAnotherAccount()
    {
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(false);

        var result = await Decorator().DeclineGrantRequestAsync(Decline);

        Assert.Equal(DecideGrantRequestResultCode.UnauthorizedError, result.ResultCode);
    }

    [Fact]
    public async Task ListGrantRequestsAsync_PassesTheProvidersScope_ForReadOnGrantRequest()
    {
        HashSet<Guid> readable = [RequestId];
        _authorization
            .Setup(p => p.GetAuthorizedResourceIdsAsync(AuthAction.Read, GRANT_REQUEST))
            .ReturnsAsync(readable);
        _inner
            .Setup(s =>
                s.ListGrantRequestsAsync(
                    It.IsAny<ListGrantRequestsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<GrantRequest>(
                    RetrieveResultCode.Success,
                    "",
                    PagedResult<GrantRequest>.Empty()
                )
            );

        await Decorator().ListGrantRequestsAsync(new([AccountId]));

        _inner.Verify(
            s =>
                s.ListGrantRequestsAsync(
                    It.IsAny<ListGrantRequestsRequest>(),
                    readable,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetGrantRequestAsync_ReturnsUnauthorized_ForNoReadOnThatRequest()
    {
        Deny(AuthAction.Read, GRANT_REQUEST);

        var result = await Decorator().GetGrantRequestAsync(AccountId, RequestId);

        Assert.Equal(RetrieveResultCode.UnauthorizedError, result.ResultCode);
    }
}
