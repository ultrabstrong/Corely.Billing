using Corely.Billing.Grants.Models;
using Corely.Billing.IAM.Authorization;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.IAM.Security.Constants;
using Corely.IAM.Security.Providers;
using static Corely.Billing.IAM.UnitTests.TestUsage;

namespace Corely.Billing.IAM.UnitTests.Authorization;

public class GrantAuthorizationDecoratorTests
{
    private const string GRANT = BillingResourceTypes.GRANT_RESOURCE_TYPE;

    private readonly Mock<IGrantService> _inner = new();
    private readonly Mock<IAuthorizationProvider> _authorization = new();

    public GrantAuthorizationDecoratorTests()
    {
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(true);
        _authorization
            .Setup(p => p.IsAuthorizedAsync(It.IsAny<AuthAction>(), GRANT, It.IsAny<Guid[]>()))
            .ReturnsAsync(true);
    }

    private GrantAuthorizationDecorator Decorator() => new(_inner.Object, _authorization.Object);

    private void Deny(AuthAction action) =>
        _authorization
            .Setup(p => p.IsAuthorizedAsync(action, GRANT, It.IsAny<Guid[]>()))
            .ReturnsAsync(false);

    private void DenyAccount() =>
        _authorization.Setup(p => p.HasAccountContext(AccountId)).Returns(false);

    private static CreateGrantRequest CreateRequest =>
        new(AccountId, Extraction, Page, 10, DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

    private static UpdateGrantRequest UpdateRequest =>
        new(AccountId, GrantId, Page, 10, DateTime.UtcNow, DateTime.UtcNow.AddDays(1));

    [Fact]
    public async Task CreateGrantAsync_CallsInner_ForCreateOnGrantInTheAccount()
    {
        _inner
            .Setup(s =>
                s.CreateGrantAsync(It.IsAny<CreateGrantRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new CreateGrantResult(CreateGrantResultCode.Success, "", GrantId));

        var result = await Decorator().CreateGrantAsync(CreateRequest);

        Assert.Equal(CreateGrantResultCode.Success, result.ResultCode);
        _authorization.Verify(p => p.IsAuthorizedAsync(AuthAction.Create, GRANT), Times.Once);
    }

    [Fact]
    public async Task CreateGrantAsync_ReturnsUnauthorized_ForNoCreatePermission()
    {
        Deny(AuthAction.Create);

        var result = await Decorator().CreateGrantAsync(CreateRequest);

        Assert.Equal(CreateGrantResultCode.UnauthorizedError, result.ResultCode);
        Assert.Equal("Unauthorized to create grant", result.Message);
        _inner.Verify(
            s => s.CreateGrantAsync(It.IsAny<CreateGrantRequest>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateGrantAsync_ReturnsUnauthorized_ForAnotherAccount()
    {
        DenyAccount();

        var result = await Decorator().CreateGrantAsync(CreateRequest);

        Assert.Equal(CreateGrantResultCode.UnauthorizedError, result.ResultCode);
        _authorization.Verify(
            p =>
                p.IsAuthorizedAsync(It.IsAny<AuthAction>(), It.IsAny<string>(), It.IsAny<Guid[]>()),
            Times.Never
        );
    }

    [Fact]
    public async Task GetGrantAsync_ChecksReadOnThatGrant_ForAGrantId()
    {
        _inner
            .Setup(s => s.GetGrantAsync(AccountId, GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new RetrieveSingleResult<Grant>(RetrieveResultCode.Success, "", new Grant())
            );

        var result = await Decorator().GetGrantAsync(AccountId, GrantId);

        Assert.Equal(RetrieveResultCode.Success, result.ResultCode);
        _authorization.Verify(
            p => p.IsAuthorizedAsync(AuthAction.Read, GRANT, GrantId),
            Times.Once
        );
    }

    [Fact]
    public async Task GetGrantAsync_ReturnsUnauthorized_ForAnotherAccount()
    {
        DenyAccount();

        var result = await Decorator().GetGrantAsync(AccountId, GrantId);

        Assert.Equal(RetrieveResultCode.UnauthorizedError, result.ResultCode);
        Assert.Equal($"Unauthorized to read grant {GrantId}", result.Message);
    }

    [Fact]
    public async Task UpdateGrantAsync_ChecksUpdateOnThatGrant_ForAGrantId()
    {
        _inner
            .Setup(s =>
                s.UpdateGrantAsync(It.IsAny<UpdateGrantRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new ModifyResult(ModifyResultCode.Success, ""));

        var result = await Decorator().UpdateGrantAsync(UpdateRequest);

        Assert.Equal(ModifyResultCode.Success, result.ResultCode);
        _authorization.Verify(
            p => p.IsAuthorizedAsync(AuthAction.Update, GRANT, GrantId),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateGrantAsync_ReturnsUnauthorized_ForNoUpdatePermission()
    {
        Deny(AuthAction.Update);

        var result = await Decorator().UpdateGrantAsync(UpdateRequest);

        Assert.Equal(ModifyResultCode.UnauthorizedError, result.ResultCode);
        Assert.Equal($"Unauthorized to update grant {GrantId}", result.Message);
    }

    [Fact]
    public async Task UpdateGrantAsync_ReturnsUnauthorized_ForAnotherAccount()
    {
        DenyAccount();

        Assert.Equal(
            ModifyResultCode.UnauthorizedError,
            (await Decorator().UpdateGrantAsync(UpdateRequest)).ResultCode
        );
    }

    [Fact]
    public async Task DeleteGrantAsync_ChecksDeleteOnThatGrant_ForAGrantId()
    {
        _inner
            .Setup(s => s.DeleteGrantAsync(AccountId, GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteGrantResult(DeleteGrantResultCode.Success, ""));

        var result = await Decorator().DeleteGrantAsync(AccountId, GrantId);

        Assert.Equal(DeleteGrantResultCode.Success, result.ResultCode);
        _authorization.Verify(
            p => p.IsAuthorizedAsync(AuthAction.Delete, GRANT, GrantId),
            Times.Once
        );
    }

    [Fact]
    public async Task DeleteGrantAsync_ReturnsUnauthorized_ForNoDeletePermission()
    {
        Deny(AuthAction.Delete);

        var result = await Decorator().DeleteGrantAsync(AccountId, GrantId);

        Assert.Equal(DeleteGrantResultCode.UnauthorizedError, result.ResultCode);
        Assert.Equal($"Unauthorized to delete grant {GrantId}", result.Message);
    }

    [Fact]
    public async Task DeleteGrantAsync_ReturnsUnauthorized_ForAnotherAccount()
    {
        DenyAccount();

        Assert.Equal(
            DeleteGrantResultCode.UnauthorizedError,
            (await Decorator().DeleteGrantAsync(AccountId, GrantId)).ResultCode
        );
    }

    [Fact]
    public async Task ListGrantsAsync_PassesTheProvidersScope_NotTheCallers()
    {
        HashSet<Guid> readable = [GrantId];
        _authorization
            .Setup(p => p.GetAuthorizedResourceIdsAsync(AuthAction.Read, GRANT))
            .ReturnsAsync(readable);
        _inner
            .Setup(s =>
                s.ListGrantsAsync(
                    It.IsAny<ListGrantsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<Grant>(
                    RetrieveResultCode.Success,
                    "",
                    PagedResult<Grant>.Empty()
                )
            );

        await Decorator()
            .ListGrantsAsync(
                new ListGrantsRequest(AccountId),
                new HashSet<Guid> { Guid.NewGuid() }
            );

        _inner.Verify(
            s =>
                s.ListGrantsAsync(
                    It.IsAny<ListGrantsRequest>(),
                    readable,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task ListGrantsAsync_ReturnsUnauthorized_ForNoReadPermission()
    {
        Deny(AuthAction.Read);

        var result = await Decorator().ListGrantsAsync(new ListGrantsRequest(AccountId));

        Assert.Equal(RetrieveResultCode.UnauthorizedError, result.ResultCode);
        Assert.Equal("Unauthorized to list grants", result.Message);
        _authorization.Verify(
            p => p.GetAuthorizedResourceIdsAsync(It.IsAny<AuthAction>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ListGrantsAsync_ReturnsUnauthorized_ForAnotherAccount()
    {
        DenyAccount();

        Assert.Equal(
            RetrieveResultCode.UnauthorizedError,
            (await Decorator().ListGrantsAsync(new ListGrantsRequest(AccountId))).ResultCode
        );
    }
}
