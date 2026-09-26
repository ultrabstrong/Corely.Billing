using Corely.Billing.Consumption.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class GrantListTests : BillingWebTestContext
{
    private IRenderedComponent<GrantList> Render(bool canManage = true) =>
        Render<GrantList>(p => p.Add(c => c.AccountId, AccountId).Add(c => c.CanManage, canManage));

    [Fact]
    public void Render_OffersTheFirstGrant_ForAnAccountWithNoneThatCanManage()
    {
        var list = Render();

        Assert.Contains("No grants yet", list.Markup);
        Assert.Equal(BillingWebRoutes.GRANT_NEW, list.Find(".cbw-empty a").GetAttribute("href"));
    }

    [Fact]
    public void Render_OffersNoCreateLink_ForAnAccountWithNoneThatCannotManage()
    {
        var list = Render(canManage: false);

        Assert.Contains("No grants yet", list.Markup);
        Assert.Empty(list.FindAll("a"));
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
        var grant = Grant(null);
        HaveGrants(grant);
        HaveUsed(new GrantTotalConsumptions(grant.GrantId, 42));

        var row = Render().Find(".cbw-grant-row");

        Assert.Contains("Unlimited pages", row.TextContent);
        Assert.Contains("42 pages used, no limit", row.TextContent);
        Assert.Empty(row.QuerySelectorAll("[role=meter]"));
    }

    [Fact]
    public void Render_ReadsAsOverdrawn_ForAGrantUsedPastItsQuantity()
    {
        var grant = Grant(10);
        HaveGrants(grant);
        HaveUsed(new GrantTotalConsumptions(grant.GrantId, 15));

        var row = Render().Find(".cbw-grant-row");

        Assert.Contains("Overdrawn by 5 pages", row.TextContent);
        Assert.NotNull(row.QuerySelector(".cbw-meter-overdrawn"));
    }

    [Fact]
    public void Render_HidesEditAndDelete_ForAHostThatCannotManage()
    {
        HaveGrants(Grant(10));

        var list = Render(canManage: false);

        Assert.Empty(list.FindAll(".cbw-grant-actions a, .cbw-grant-actions button"));
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
}
