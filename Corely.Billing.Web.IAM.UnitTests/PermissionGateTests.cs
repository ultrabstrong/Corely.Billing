using Corely.Billing.Consumption.Models;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.IAM;
using Corely.Billing.Models;
using Corely.Billing.Services;
using Corely.Billing.Usage;
using Corely.Billing.Web.Components;
using Corely.Billing.Web.IAM.Extensions;
using Corely.IAM.Security.Enums;
using Corely.IAM.Security.Providers;
using Corely.IAM.Users.Models;
using Corely.IAM.Users.Providers;
using Corely.IAM.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Corely.Billing.Web.IAM.UnitTests;

public class PermissionGateTests : BunitContext
{
    private const string GRANT = BillingResourceTypes.GRANT_RESOURCE_TYPE;
    private const string GRANT_REQUEST = BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE;

    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly UsageOperation Extraction = UsageOperation.From("extraction");
    private static readonly UsageUnit Page = UsageUnit.From("page");

    private readonly Mock<IGrantService> _grants = new();
    private readonly Mock<IGrantRequestService> _grantRequests = new();
    private readonly Mock<IAuthorizationProvider> _authorization = new();
    private readonly HashSet<AuthAction> _allowed = [];
    private readonly HashSet<AuthAction> _allowedOnRequests = [];
    private readonly List<GrantRequest> _requests = [];
    private readonly Grant _grant = new()
    {
        GrantId = Guid.CreateVersion7(),
        AccountId = AccountId,
        Operation = Extraction,
        Unit = Page,
        Quantity = 100,
        ValidFromUtc = Now.AddDays(-1),
        ValidToUtc = Now.AddDays(30),
    };

    public PermissionGateTests()
    {
        var consumption = new Mock<IConsumptionService>();
        consumption
            .Setup(c =>
                c.GetGrantConsumptionTotalsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveSingleResult<List<GrantTotalConsumptions>>(
                    RetrieveResultCode.Success,
                    "",
                    []
                )
            );
        _grants
            .Setup(g =>
                g.ListGrantsAsync(
                    It.IsAny<ListGrantsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<Grant>(
                    RetrieveResultCode.Success,
                    "",
                    new PagedResult<Grant>([_grant], 1, 1, false)
                )
            );
        _grantRequests
            .Setup(g =>
                g.ListGrantRequestsAsync(
                    It.IsAny<ListGrantRequestsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(() =>
                new RetrieveListResult<GrantRequest>(
                    RetrieveResultCode.Success,
                    "",
                    new PagedResult<GrantRequest>([.. _requests], _requests.Count, 1, false)
                )
            );
        _grants
            .Setup(g => g.GetGrantAsync(AccountId, _grant.GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrieveSingleResult<Grant>(RetrieveResultCode.Success, "", _grant));
        _authorization
            .Setup(p => p.IsAuthorizedAsync(It.IsAny<AuthAction>(), GRANT, It.IsAny<Guid[]>()))
            .ReturnsAsync((AuthAction action, string _, Guid[] _) => _allowed.Contains(action));
        _authorization
            .Setup(p =>
                p.IsAuthorizedAsync(It.IsAny<AuthAction>(), GRANT_REQUEST, It.IsAny<Guid[]>())
            )
            .ReturnsAsync(
                (AuthAction action, string _, Guid[] _) => _allowedOnRequests.Contains(action)
            );

        var userContext = new UserContext(
            new User { Id = Guid.CreateVersion7(), Username = "reader" },
            null,
            "device",
            []
        );
        var userContextProvider = new Mock<IUserContextProvider>();
        userContextProvider.Setup(p => p.GetUserContext()).Returns(userContext);
        var userContextAccessor = new Mock<IBlazorUserContextAccessor>();
        userContextAccessor.Setup(a => a.GetUserContextAsync()).ReturnsAsync(userContext);

        var vocabulary = new Mock<IUsageVocabulary>();
        vocabulary.Setup(v => v.Operations).Returns([new(Extraction, "Extraction")]);
        vocabulary.Setup(v => v.Units).Returns([new(Page, "page")]);
        vocabulary.Setup(v => v.DisplayName(It.IsAny<UsageOperation>())).Returns("Extraction");
        vocabulary.Setup(v => v.DisplayName(It.IsAny<UsageUnit>())).Returns("page");

        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddOptions();
        Services.AddBillingWebIam();
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now));
        Services.AddSingleton(_grants.Object);
        Services.AddSingleton(_grantRequests.Object);
        Services.AddSingleton(consumption.Object);
        Services.AddSingleton(vocabulary.Object);
        Services.AddSingleton(_authorization.Object);
        Services.AddSingleton(userContextProvider.Object);
        Services.AddSingleton(userContextAccessor.Object);
    }

    private void Allow(params AuthAction[] actions) => _allowed.UnionWith(actions);

    private IRenderedComponent<GrantList> RenderList() =>
        Render<GrantList>(p => p.Add(c => c.AccountId, AccountId));

    [Fact]
    public void GrantList_ShowsEditOnly_ForReadAndUpdateWithoutCreateOrDelete()
    {
        Allow(AuthAction.Read, AuthAction.Update);

        var list = RenderList();

        Assert.Empty(list.FindAll(".cbw-toolbar a"));
        Assert.Empty(list.FindAll("button[aria-label='Delete grant']"));
        Assert.Single(list.FindAll("a[aria-label='Edit grant']"));
        Assert.Empty(list.FindAll("a[aria-label='View grant']"));
    }

    [Fact]
    public void GrantList_ShowsViewLinksOnly_ForReadAlone()
    {
        Allow(AuthAction.Read);

        var list = RenderList();

        Assert.Single(list.FindAll("a[aria-label='View grant']"));
        Assert.Empty(list.FindAll("a[aria-label='Edit grant']"));
        Assert.Empty(list.FindAll("button[aria-label='Delete grant']"));
        Assert.Empty(list.FindAll(".cbw-toolbar a"));
    }

    [Fact]
    public void GrantList_ShowsEveryControl_ForCreateUpdateAndDelete()
    {
        Allow(AuthAction.Read, AuthAction.Create, AuthAction.Update, AuthAction.Delete);

        var list = RenderList();

        Assert.Single(list.FindAll(".cbw-toolbar a"));
        Assert.Single(list.FindAll("a[aria-label='Edit grant']"));
        Assert.Single(list.FindAll("button[aria-label='Delete grant']"));
    }

    [Fact]
    public void GrantList_AsksIamAboutThatGrant_ForUpdateAndDelete()
    {
        RenderList();

        _authorization.Verify(p => p.IsAuthorizedAsync(AuthAction.Create, GRANT), Times.Once);
        _authorization.Verify(
            p => p.IsAuthorizedAsync(AuthAction.Update, GRANT, _grant.GrantId),
            Times.Once
        );
        _authorization.Verify(
            p => p.IsAuthorizedAsync(AuthAction.Delete, GRANT, _grant.GrantId),
            Times.Once
        );
    }

    [Fact]
    public void GrantList_OffersRequestAlone_ForCreateOnGrantRequestWithoutCreateOnGrant()
    {
        Allow(AuthAction.Read);
        _allowedOnRequests.UnionWith([AuthAction.Read, AuthAction.Create]);

        var toolbar = RenderList().FindAll(".cbw-toolbar a");

        Assert.Equal([BillingWebRoutes.GRANT_REQUEST], toolbar.Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void GrantList_ShowsNoDecision_ForUpdateOnGrantRequestWithoutCreateOnGrant()
    {
        Allow(AuthAction.Read);
        _allowedOnRequests.UnionWith([AuthAction.Read, AuthAction.Update]);
        _requests.Add(PendingRequest());

        var list = RenderList();

        Assert.Single(list.FindAll(".cbw-request-row"));
        Assert.Empty(list.FindAll("button[aria-label='Approve request']"));
    }

    [Fact]
    public void GrantList_ShowsTheDecision_ForUpdateOnGrantRequestAndCreateOnGrant()
    {
        var request = PendingRequest();
        Allow(AuthAction.Read, AuthAction.Create);
        _allowedOnRequests.UnionWith([AuthAction.Read, AuthAction.Update]);
        _requests.Add(request);

        var list = RenderList();

        Assert.Single(list.FindAll("button[aria-label='Approve request']"));
        Assert.Single(list.FindAll("button[aria-label='Decline request']"));
        _authorization.Verify(
            p => p.IsAuthorizedAsync(AuthAction.Update, GRANT_REQUEST, request.GrantRequestId),
            Times.Once
        );
    }

    private static GrantRequest PendingRequest() =>
        new()
        {
            GrantRequestId = Guid.CreateVersion7(),
            AccountId = AccountId,
            Operation = Extraction,
            Unit = Page,
            Quantity = 500,
            RequestedUtc = Now.AddDays(-1),
            Status = GrantRequestStatus.Pending,
        };

    [Fact]
    public void GrantEditor_ShowsTheGrantReadOnly_ForReadWithoutUpdate()
    {
        Allow(AuthAction.Read);

        var editor = Render<GrantEditor>(p =>
            p.Add(c => c.AccountId, AccountId).Add(c => c.GrantId, _grant.GrantId)
        );

        Assert.True(editor.Find("fieldset").HasAttribute("disabled"));
        Assert.Empty(editor.FindAll("button[type=submit]"));
    }

    [Fact]
    public void GrantEditor_ShowsAnEditableForm_ForUpdate()
    {
        Allow(AuthAction.Read, AuthAction.Update);

        var editor = Render<GrantEditor>(p =>
            p.Add(c => c.AccountId, AccountId).Add(c => c.GrantId, _grant.GrantId)
        );

        Assert.False(editor.Find("fieldset").HasAttribute("disabled"));
        Assert.Single(editor.FindAll("button[type=submit]"));
    }
}
