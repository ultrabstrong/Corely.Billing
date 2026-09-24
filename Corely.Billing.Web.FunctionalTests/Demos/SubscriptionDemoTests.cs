extern alias SubscriptionDemo;

using System.Net;
using System.Text.RegularExpressions;
using SubscriptionDemo::Corely.Billing.Demos.Subscription;

namespace Corely.Billing.Web.FunctionalTests.Demos;

public sealed partial class SubscriptionDemoTests : IDisposable
{
    private readonly DemoAppFactory<Membership> _factory = new();
    private readonly HttpClient _client;

    public SubscriptionDemoTests()
    {
        _client = _factory.CreateTestClient();
        _factory.CreateBillingSchema();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task MembersArea_OpensOnlyDuringATerm_ForSubscribeThenCancel()
    {
        Assert.Equal("/", await MembersRedirectAsync());

        await PostAsync("Subscribe");
        var firstVisit = await GetAsync("/members");
        var secondVisit = await GetAsync("/members");

        await PostAsync("Cancel");

        Assert.Contains("visited once this term", firstVisit);
        Assert.Contains("visited 2 times this term", secondVisit);
        Assert.Equal("/", await MembersRedirectAsync());
    }

    private async Task<string?> MembersRedirectAsync()
    {
        using var response = await _client.GetAsync("/members", Token);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.Location?.OriginalString;
    }

    private async Task<string> GetAsync(string path)
    {
        using var response = await _client.GetAsync(path, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(Token);
    }

    private async Task PostAsync(string handler)
    {
        var page = await GetAsync("/");
        var token = AntiforgeryToken().Match(page).Groups[1].Value;
        using var response = await _client.PostAsync(
            $"/?handler={handler}",
            new FormUrlEncodedContent(
                new Dictionary<string, string> { ["__RequestVerificationToken"] = token }
            ),
            Token
        );
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
