using Corely.Billing.Consumption.Models;
using Corely.Billing.GrantRequests.Enums;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class GrantListTests : BillingWebTestContext
{
    private const string NEW_GRANT_HREF = "/host/grants/new";
    private const string REQUEST_GRANT_HREF = "/host/grants/request";

    private IRenderedComponent<GrantList> Render() =>
        Render<GrantList>(p =>
            p.Add(c => c.AccountId, AccountId)
                .Add(c => c.NewGrantHref, NEW_GRANT_HREF)
                .Add(c => c.RequestGrantHref, REQUEST_GRANT_HREF)
                .Add(c => c.GrantHref, id => $"/host/grants/{id}")
        );

    [Fact]
    public void Render_OffersTheFirstGrant_ForAnEmptyAccountAllowedToCreate()
    {
        var list = Render();

        Assert.Contains("No grants yet", list.Markup);
        Assert.Equal(NEW_GRANT_HREF, list.Find(".cbw-empty a").GetAttribute("href"));
    }

    [Fact]
    public void Render_OffersNoCreateLink_ForAnEmptyAccountNotAllowedToCreate()
    {
        ActionGate.Deny(GrantAction.Create, GrantAction.Request);
        var list = Render();

        Assert.Contains("No grants yet", list.Markup);
        Assert.Contains("Grants given to this account will appear here.", list.Markup);
        Assert.Empty(list.FindAll("a"));
        Assert.Equal(
            [(GrantAction.Create, (Guid?)null), (GrantAction.Request, null)],
            ActionGate.Calls
        );
    }

    [Fact]
    public void Render_OffersARequest_ForAnEmptyAccountAllowedOnlyToRequest()
    {
        ActionGate.Deny(GrantAction.Create);

        var links = Render().FindAll(".cbw-empty a");

        Assert.Equal([REQUEST_GRANT_HREF], links.Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void Render_ShowsAPendingRequestAboveTheGrants_ForAnAccountWithBoth()
    {
        HaveGrants(Grant(10));
        HaveRequests(Request(5_000, note: "Month end batch"));

        var rows = Render().FindAll(".cbw-grant-row");

        Assert.Equal(2, rows.Count);
        Assert.Contains("cbw-request-row", rows[0].ClassName);
        Assert.Contains("5,000 pages", rows[0].TextContent);
        Assert.Contains("Month end batch", rows[0].TextContent);
        Assert.Contains("Requested 2 days ago", rows[0].TextContent);
        Assert.Contains("Requested", rows[0].QuerySelector(".badge")!.TextContent);
    }

    [Fact]
    public void Render_ListsOnlyPendingRequests_ForTheRequestQuery()
    {
        Render();

        GrantRequests.Verify(g =>
            g.ListGrantRequestsAsync(
                It.Is<ListGrantRequestsRequest>(r =>
                    r.AccountIds.SequenceEqual(new[] { AccountId })
                    && r.Status == GrantRequestStatus.Pending
                ),
                null,
                It.IsAny<CancellationToken>()
            )
        );
    }

    [Fact]
    public void Render_ShowsGrantsAndNoError_ForARefusedRequestRead()
    {
        HaveGrants(Grant(10));
        DenyRequests();

        var list = Render();

        Assert.Single(list.FindAll(".cbw-grant-row"));
        Assert.Empty(list.FindAll(".alert-danger"));
    }

    [Fact]
    public void Render_HidesTheRequestButton_ForEveryOperationPending()
    {
        HaveRequests([
            .. new[] { Extraction, Members, Retention, Sftp }.Select(operation =>
            {
                var request = Request(5);
                request.Operation = operation;
                return request;
            }),
        ]);

        var toolbar = Render().FindAll(".cbw-toolbar a");

        Assert.DoesNotContain(REQUEST_GRANT_HREF, toolbar.Select(a => a.GetAttribute("href")));
    }

    [Fact]
    public void Approve_ConfirmsThenApprovesForAYearFromToday_ForARequest()
    {
        var request = Request(5_000);
        HaveRequests(request);
        GrantRequests
            .Setup(g =>
                g.ApproveGrantRequestAsync(
                    It.IsAny<ApproveGrantRequestRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApproveGrantRequestResult(
                    DecideGrantRequestResultCode.Success,
                    string.Empty,
                    Guid.CreateVersion7()
                )
            );
        var list = Render();

        list.Find("button[aria-label='Approve request']").Click();
        Assert.Contains("Approve 5,000 pages for a year from today?", list.Markup);
        list.Find(".btn-success").Click();

        GrantRequests.Verify(
            g =>
                g.ApproveGrantRequestAsync(
                    new ApproveGrantRequestRequest(
                        AccountId,
                        request.GrantRequestId,
                        Now.Date,
                        Now.Date.AddYears(1)
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Decline_SendsTheReason_ForADeclinedRequest()
    {
        var request = Request(5_000);
        HaveRequests(request);
        GrantRequests
            .Setup(g =>
                g.DeclineGrantRequestAsync(
                    It.IsAny<DeclineGrantRequestRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new DeclineGrantRequestResult(DecideGrantRequestResultCode.Success, string.Empty)
            );
        var list = Render();

        list.Find("button[aria-label='Decline request']").Click();
        list.Find(".cbw-decline-reason").Change("Use the annual plan");
        list.Find(".cbw-grant-actions .btn-danger").Click();

        GrantRequests.Verify(
            g =>
                g.DeclineGrantRequestAsync(
                    new DeclineGrantRequestRequest(
                        AccountId,
                        request.GrantRequestId,
                        "Use the annual plan"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public void Approve_ReloadsAndSaysSo_ForARequestSomeoneElseDecided()
    {
        HaveRequests(Request(5_000));
        GrantRequests
            .Setup(g =>
                g.ApproveGrantRequestAsync(
                    It.IsAny<ApproveGrantRequestRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new ApproveGrantRequestResult(
                    DecideGrantRequestResultCode.AlreadyDecidedError,
                    "decided",
                    Guid.Empty
                )
            );
        var list = Render();

        list.Find("button[aria-label='Approve request']").Click();
        list.Find(".btn-success").Click();

        Assert.Equal(
            "Someone else already decided that request.",
            list.Find(".alert-danger").TextContent
        );
        GrantRequests.Verify(
            g =>
                g.ListGrantRequestsAsync(
                    It.IsAny<ListGrantRequestsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public void Render_ShowsAMessage_ForAnUnauthorizedList()
    {
        Grants
            .Setup(g =>
                g.ListGrantsAsync(
                    It.IsAny<ListGrantsRequest>(),
                    It.IsAny<IReadOnlySet<Guid>?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<Grant>(RetrieveResultCode.UnauthorizedError, "no", null)
            );

        var list = Render();

        Assert.Equal("You are not allowed to view grants.", list.Find(".alert-danger").TextContent);
        Assert.DoesNotContain("No grants yet", list.Markup);
    }

    [Fact]
    public void Render_LabelsEachStatus_ForUpcomingActiveAndExpiredGrants()
    {
        HaveGrants(
            Grant(100, fromDays: 5, toDays: 40),
            Grant(100),
            Grant(100, fromDays: -40, toDays: -5)
        );

        var list = Render();

        Assert.Equal(
            ["Upcoming", "Active", "Expired"],
            list.FindAll(".cbw-grant-status .badge").Select(b => b.TextContent.Trim())
        );
    }

    [Fact]
    public void Render_ShowsDisplayNamesNotTokens_ForAGrant()
    {
        HaveGrants(Grant(1));

        var row = Render().Find(".cbw-grant-row");

        Assert.Contains("Document extraction", row.TextContent);
        Assert.Contains("1 page", row.TextContent);
        Assert.DoesNotContain("document_extraction", row.TextContent);
    }

    [Fact]
    public void Render_ShowsUnlimitedAndNoMeter_ForAGrantWithNoQuantity()
    {
        HaveGrants(Grant(null, used: 42));

        var row = Render().Find(".cbw-grant-row");

        Assert.Contains("Unlimited pages", row.TextContent);
        Assert.Contains("Unlimited, 42 pages used", row.TextContent);
        Assert.Empty(row.QuerySelectorAll("[role=meter]"));
    }

    [Fact]
    public void Render_ReadsAsOverdrawn_ForAGrantUsedPastItsQuantity()
    {
        HaveGrants(Grant(10, used: 15));

        var row = Render().Find(".cbw-grant-row");

        Assert.Contains("Overdrawn by 5 pages", row.TextContent);
        Assert.NotNull(row.QuerySelector(".cbw-meter-overdrawn"));
    }

    [Theory]
    [InlineData("allowance", "+60 days, until 31 March 2026")]
    [InlineData("feature", "Enabled, until 31 March 2026")]
    [InlineData("capacity", "2 of 7 users in use")]
    public void Render_ShowsItsOwnStateAndNoMeter_ForEachGrantType(string type, string expected)
    {
        HaveGrants(
            type switch
            {
                "allowance" => Retained(60),
                "feature" => Feature(),
                _ => Capacity(7, inUse: 2),
            }
        );

        var row = Render().Find(".cbw-grant-row");

        Assert.Contains(expected, row.QuerySelector(".cbw-grant-state")!.TextContent);
        Assert.Empty(row.QuerySelectorAll("[role=meter]"));
        Assert.DoesNotContain("left of", row.TextContent);
    }

    [Fact]
    public void Render_AsksTheGateForEachAction_ForARow()
    {
        var grant = Grant(10);
        HaveGrants(grant);

        Render();

        Assert.Equal(
            [
                (GrantAction.Request, (Guid?)null),
                (GrantAction.Create, null),
                (GrantAction.Update, grant.GrantId),
                (GrantAction.Delete, grant.GrantId),
            ],
            ActionGate.Calls
        );
    }

    [Fact]
    public void Render_ShowsEditOnly_ForUpdateWithoutCreateOrDelete()
    {
        HaveGrants(Grant(10));
        ActionGate.Deny(GrantAction.Create, GrantAction.Delete, GrantAction.Request);

        var list = Render();

        Assert.Empty(list.FindAll(".cbw-toolbar a"));
        Assert.Empty(list.FindAll("button[aria-label='Delete grant']"));
        Assert.Single(list.FindAll("a[aria-label='Edit grant']"));
    }

    [Fact]
    public void Render_OffersViewInsteadOfEdit_ForNoUpdate()
    {
        var grant = Grant(10);
        HaveGrants(grant);
        ActionGate.Deny(GrantAction.Update);

        var view = Render().Find("a[aria-label='View grant']");

        Assert.Equal($"/host/grants/{grant.GrantId}", view.GetAttribute("href"));
    }

    [Fact]
    public void Delete_AsksFirstThenDeletes_ForAConfirmedDelete()
    {
        var grant = Grant(10);
        HaveGrants(grant);
        Grants
            .Setup(g => g.DeleteGrantAsync(AccountId, grant.GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteGrantResult(DeleteGrantResultCode.Success, string.Empty));
        var list = Render();

        list.Find("button[aria-label='Delete grant']").Click();
        Grants.Verify(
            g =>
                g.DeleteGrantAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        list.Find(".btn-danger").Click();

        Grants.Verify(
            g => g.DeleteGrantAsync(AccountId, grant.GrantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        Assert.Empty(list.FindAll(".cbw-grant-row"));
    }

    [Fact]
    public void Delete_SaysWhatItReleases_ForACapacityGrantWithRegistrations()
    {
        HaveGrants(Capacity(7, inUse: 3));
        var list = Render();

        list.Find("button[aria-label='Delete grant']").Click();

        Assert.Contains(
            "Delete this grant and release the 3 users registered on it?",
            list.Find(".cbw-confirm-text").TextContent
        );
    }

    [Fact]
    public void Render_GroupsRowsUnderTheirKind_InKindOrder()
    {
        var feature = Feature();
        var balance = Grant(100);
        var allowance = Retained(60);
        var capacity = Capacity(7, inUse: 2);
        HaveGrants(feature, balance, allowance, capacity);
        HaveRequests(Request(5_000));

        var list = Render();

        Assert.Equal(
            [
                "Balance",
                "request",
                balance.GrantId.ToString(),
                "Capacity",
                capacity.GrantId.ToString(),
                "Allowance",
                allowance.GrantId.ToString(),
                "Feature",
                feature.GrantId.ToString(),
            ],
            list.FindAll(".cbw-grant-group, .cbw-grant-row")
                .Select(item =>
                    item.ClassList.Contains("cbw-grant-group") ? item.TextContent.Trim()
                    : item.ClassList.Contains("cbw-request-row") ? "request"
                    : item.GetAttribute("data-grant-id")
                )
        );
    }
}
