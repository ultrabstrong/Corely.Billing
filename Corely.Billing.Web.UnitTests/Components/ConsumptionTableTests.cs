using Corely.Billing.Consumption.Models;
using Corely.Billing.Models;
using Corely.Billing.Web.Components;
using Corely.Common.Filtering.Ordering;

namespace Corely.Billing.Web.UnitTests.Components;

public class ConsumptionTableTests : BillingWebTestContext
{
    private readonly List<ListConsumptionEventsRequest> _requests = [];

    public ConsumptionTableTests()
    {
        var events = Enumerable
            .Range(0, 30)
            .Select(i => new ConsumptionEvent
            {
                AccountId = AccountId,
                GrantId = Guid.CreateVersion7(),
                Operation = Extraction,
                Unit = Page,
                Quantity = i,
                Provider = "demo",
                UtcTimestamp = Now.AddHours(-i),
                Outcome = ConsumptionOutcome.Settled,
            })
            .ToList();

        Consumption
            .Setup(c =>
                c.ListConsumptionEventsAsync(
                    It.IsAny<ListConsumptionEventsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<ListConsumptionEventsRequest, CancellationToken>((r, _) => _requests.Add(r))
            .ReturnsAsync(
                (ListConsumptionEventsRequest r, CancellationToken _) =>
                    new RetrieveListResult<ConsumptionEvent>(
                        RetrieveResultCode.Success,
                        string.Empty,
                        PagedResult<ConsumptionEvent>.Create(
                            [.. events.Skip(r.Skip).Take(r.Take)],
                            events.Count,
                            r.Skip,
                            r.Take
                        )
                    )
            );
    }

    private static readonly UsageFilter Filter = new(
        Now.AddDays(-7),
        Now,
        Units: [Page],
        Operations: [Extraction],
        Providers: ["demo"],
        GrantIds: [Guid.Parse("22222222-2222-2222-2222-222222222222")]
    );

    private IRenderedComponent<ConsumptionTable> Render() =>
        Render<ConsumptionTable>(p =>
            p.Add(c => c.AccountId, AccountId).Add(c => c.Filter, Filter).Add(c => c.PageSize, 10)
        );

    [Fact]
    public void Render_PassesEveryFilter_ForTheFirstPage()
    {
        Render();

        var request = Assert.Single(_requests);
        Assert.Equal(AccountId, request.AccountId);
        Assert.Equal(Filter.FromUtc, request.FromUtc);
        Assert.Equal(Filter.ToUtc, request.ToUtc);
        Assert.Equal(Filter.Units, request.Units);
        Assert.Equal(Filter.Operations, request.Operations);
        Assert.Equal(Filter.Providers, request.Providers);
        Assert.Equal(Filter.GrantIds, request.GrantIds);
        Assert.Equal((0, 10), (request.Skip, request.Take));
    }

    [Fact]
    public void Sort_FlipsTheDirection_ForASecondClickOnOneColumn()
    {
        var table = Render();

        table.FindAll("button.cbw-sort")[1].Click();
        table.FindAll("button.cbw-sort")[1].Click();

        Assert.Equal(
            [
                (ConsumptionEventSortField.UtcTimestamp, SortDirection.Descending),
                (ConsumptionEventSortField.Quantity, SortDirection.Descending),
                (ConsumptionEventSortField.Quantity, SortDirection.Ascending),
            ],
            _requests.Select(r => (r.SortBy, r.SortDirection))
        );
    }

    [Fact]
    public void Page_AdvancesBySkip_ForTheNextPageButton()
    {
        var table = Render();

        table.Find("button[aria-label='Next page']").Click();

        Assert.Equal(10, _requests[^1].Skip);
        Assert.Contains("11–20 of 30", table.Find(".cbw-pager").TextContent);
    }

    [Fact]
    public void Render_ShowsAnEmptyState_ForNoEvents()
    {
        Consumption
            .Setup(c =>
                c.ListConsumptionEventsAsync(
                    It.IsAny<ListConsumptionEventsRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new RetrieveListResult<ConsumptionEvent>(
                    RetrieveResultCode.Success,
                    string.Empty,
                    PagedResult<ConsumptionEvent>.Empty()
                )
            );

        Assert.Contains("Nothing was used in this range.", Render().Markup);
    }
}
