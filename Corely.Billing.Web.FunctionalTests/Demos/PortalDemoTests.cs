extern alias PortalDemo;

using System.Net;
using PortalDemo::Corely.Billing.Demos.Portal.Components;

namespace Corely.Billing.Web.FunctionalTests.Demos;

public sealed class PortalDemoTests : IDisposable
{
    private readonly DemoAppFactory<App> _factory = new();
    private readonly HttpClient _client;

    public PortalDemoTests()
    {
        _client = _factory.CreateTestClient();
        _factory.CreateBillingSchema();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData("/")]
    [InlineData(BillingWebRoutes.GRANTS)]
    [InlineData(BillingWebRoutes.GRANT_NEW)]
    [InlineData(BillingWebRoutes.USAGE)]
    public async Task EveryPageAnswers_ForTheHomePageAndTheLibrarysRoutedPages(string path)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("_content/Corely.Billing.Web/lib/chart.js/chart.umd.min.js", "Chart.js")]
    [InlineData("_content/Corely.Billing.Web/Components/UsageChart.razor.js", "renderUsage")]
    [InlineData("Corely.Billing.Demos.Portal.styles.css", "@import '_content/Corely.Billing.Web/")]
    [InlineData("_content/Corely.Billing.Web/Corely.Billing.Web.bundle.scp.css", ".cbw-grant-row")]
    public async Task StaticAssetsAreServed_ForTheChartAndTheComponentStyles(
        string path,
        string expected
    )
    {
        var body = await _client.GetStringAsync(path, TestContext.Current.CancellationToken);

        Assert.Contains(expected, body);
    }
}
