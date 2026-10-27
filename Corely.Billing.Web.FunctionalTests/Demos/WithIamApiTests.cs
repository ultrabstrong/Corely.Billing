extern alias WithIamDemo;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WithIamDemo::Corely.Billing.Demos.WithIAM;
using WithIamDemo::Corely.Billing.Demos.WithIAM.Components;

namespace Corely.Billing.Web.FunctionalTests.Demos;

public sealed class WithIamApiTests : IAsyncLifetime
{
    private const string OPERATION = "/api/billing/operations/text_generation";

    private readonly DemoAppFactory<App> _factory = new();
    private HttpClient _client = null!;
    private string _apiKey = null!;

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateTestClient();
        _factory.CreateBillingSchema();
        _factory.CreateIamSchema();
        _apiKey = (await DemoSeed.RunAsync(_factory.Services))!;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Balance_IsRefused_ForARequestWithoutAToken()
    {
        using var response = await _client.GetAsync($"{OPERATION}/balance", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task Balance_IsTheSeededGrantsLessWhatWasUsed_ForTheKey()
    {
        await AuthorizeAsync();

        using var response = await _client.GetAsync($"{OPERATION}/balance", Ct);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.InRange(body.GetProperty("remaining").GetInt64(), 1, 2_250_000 - 1);
    }

    [Fact]
    public async Task ReserveThenSettle_Succeeds_ForTheKey()
    {
        await AuthorizeAsync();
        var operationId = Guid.CreateVersion7();

        using var reserve = await _client.PostAsJsonAsync(
            $"{OPERATION}/reserve",
            new
            {
                operationId,
                unit = "token",
                quantity = 1_000,
                provider = "api-test",
            },
            Ct
        );
        using var settle = await _client.PostAsJsonAsync(
            $"{OPERATION}/settle",
            new
            {
                operationId,
                unit = "token",
                actualQuantity = 800,
            },
            Ct
        );

        Assert.True(reserve.IsSuccessStatusCode, await reserve.Content.ReadAsStringAsync(Ct));
        Assert.True(settle.IsSuccessStatusCode, await settle.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Reserve_IsABadRequest_ForABodyWithoutAnOperationId()
    {
        await AuthorizeAsync();

        using var response = await _client.PostAsJsonAsync(
            $"{OPERATION}/reserve",
            new
            {
                unit = "token",
                quantity = 1_000,
                provider = "api-test",
            },
            Ct
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Grants_AreForbidden_ForAKeyWithoutGrantRead()
    {
        await AuthorizeAsync();

        using var response = await _client.GetAsync("/api/billing/grants", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Balance_IsABadRequest_ForAMalformedOperation()
    {
        await AuthorizeAsync();

        using var response = await _client.GetAsync(
            "/api/billing/operations/Text-Generation/balance",
            Ct
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task AuthorizeAsync()
    {
        using var exchange = await _client.PostAsJsonAsync(
            "/api/auth/apikey",
            new { apiKey = _apiKey },
            Ct
        );
        exchange.EnsureSuccessStatusCode();
        var pair = await exchange.Content.ReadFromJsonAsync<JsonElement>(Ct);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            pair.GetProperty("authToken").GetString()
        );
    }
}
