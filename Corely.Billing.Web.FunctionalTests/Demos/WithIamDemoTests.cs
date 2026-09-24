extern alias WithIamDemo;

using System.Net;
using WithIamDemo::Corely.Billing.Demos.WithIAM.Components;

namespace Corely.Billing.Web.FunctionalTests.Demos;

public sealed class WithIamDemoTests : IDisposable
{
    private readonly DemoAppFactory<App> _factory = new();
    private readonly HttpClient _client;

    public WithIamDemoTests() => _client = _factory.CreateTestClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task SignInPageUsesTheAppsLayout_ForAnAnonymousVisitor()
    {
        var html = await _client.GetStringAsync("/signin", TestContext.Current.CancellationToken);

        Assert.Contains("Acme Extraction", html);
        Assert.Contains("_content/Corely.IAM.Web/js/form-busy.js", html);
    }

    [Theory]
    [InlineData("_content/Corely.Billing.Web/lib/chart.js/chart.umd.min.js")]
    [InlineData("_content/Corely.IAM.Web/css/iam-web.css")]
    public async Task BothLibrariesServeTheirAssets_ForOneHost(string path)
    {
        using var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
