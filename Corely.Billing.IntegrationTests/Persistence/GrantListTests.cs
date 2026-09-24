using Corely.Billing.Grants.Models;
using Corely.Billing.IntegrationTests.Infrastructure;
using Corely.Billing.Services;
using Corely.Common.Filtering;
using Corely.Common.Filtering.Filters;
using Corely.Common.Filtering.Ordering;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.IntegrationTests.Persistence;

public sealed class GrantListTests : IDisposable
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly BillingTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private async Task SeedAsync(params long?[] quantities)
    {
        for (var i = 0; i < quantities.Length; i++)
        {
            var validFrom = Start.AddDays(i);
            await _host.WithScopeAsync(services =>
                services
                    .GetRequiredService<IGrantService>()
                    .CreateGrantAsync(
                        new CreateGrantRequest(
                            AccountId,
                            TestUsage.Extraction,
                            TestUsage.Page,
                            quantities[i],
                            validFrom,
                            validFrom.AddYears(1)
                        )
                    )
            );
        }
    }

    private Task<Corely.Billing.Models.RetrieveListResult<Grant>> ListAsync(
        ListGrantsRequest request
    ) =>
        _host.WithScopeAsync(services =>
            services.GetRequiredService<IGrantService>().ListGrantsAsync(request)
        );

    [Fact]
    public async Task ListGrantsAsync_ReturnsNewestFirst_ForTheDefaultOrder()
    {
        await SeedAsync(10, 20, 30);

        var result = await ListAsync(new ListGrantsRequest(AccountId));

        Assert.Equal([30, 20, 10], result.Data!.Items.Select(g => g.Quantity));
    }

    [Fact]
    public async Task ListGrantsAsync_PagesAndCounts_ForASkipAndTake()
    {
        await SeedAsync(10, 20, 30, 40, 50);

        var result = await ListAsync(new ListGrantsRequest(AccountId, Skip: 2, Take: 2));

        Assert.Equal(5, result.Data!.TotalCount);
        Assert.Equal(2, result.Data.CurrentPage);
        Assert.True(result.Data.HasMore);
        Assert.Equal([30, 20], result.Data.Items.Select(g => g.Quantity));
    }

    [Fact]
    public async Task ListGrantsAsync_TranslatesTheFilter_ForAQuantityComparison()
    {
        await SeedAsync(10, null, 30, 40);

        var result = await ListAsync(
            new ListGrantsRequest(
                AccountId,
                Filter: Filter
                    .For<Grant>()
                    .Where(g => g.Quantity, ComparableFilter<long>.GreaterThanOrEqual(25))
            )
        );

        Assert.Equal(2, result.Data!.TotalCount);
        Assert.All(result.Data.Items, g => Assert.True(g.Quantity >= 25));
    }

    [Fact]
    public async Task ListGrantsAsync_TranslatesTheFilter_ForAnUnlimitedFilter()
    {
        await SeedAsync(10, null, 30, null);

        var result = await ListAsync(
            new ListGrantsRequest(
                AccountId,
                Filter: Filter.For<Grant>().Where(g => g.Quantity, ComparableFilter<long>.IsNull())
            )
        );

        Assert.Equal(2, result.Data!.TotalCount);
        Assert.All(result.Data.Items, g => Assert.Null(g.Quantity));
    }

    [Fact]
    public async Task ListGrantsAsync_AppliesTheOrder_ForAnExplicitOrder()
    {
        await SeedAsync(30, 10, 20);

        var result = await ListAsync(
            new ListGrantsRequest(
                AccountId,
                Order: Order.For<Grant>().By(g => g.Quantity, SortDirection.Ascending)
            )
        );

        Assert.Equal([10, 20, 30], result.Data!.Items.Select(g => g.Quantity));
    }

    [Fact]
    public async Task ListGrantsAsync_ReturnsNothing_ForAnotherAccount()
    {
        await SeedAsync(10);

        var result = await ListAsync(new ListGrantsRequest(Guid.CreateVersion7()));

        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.TotalCount);
    }
}
