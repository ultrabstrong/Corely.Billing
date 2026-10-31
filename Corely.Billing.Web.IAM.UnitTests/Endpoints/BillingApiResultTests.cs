using System.Text.Json;
using Corely.Billing.Web.IAM.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Corely.Billing.Web.IAM.UnitTests.Endpoints;

public class BillingApiResultTests
{
    private enum Code
    {
        Success,
        NoCapacityError,
    }

    private sealed record Balance(Code ResultCode, string Message, long Remaining);

    private sealed record Listing(Code ResultCode, string Message, int[]? Data);

    private static async Task<(int Status, JsonElement Body)> ExecuteAsync<T>(
        T result,
        Code code,
        string message,
        int status
    )
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging()
                .Configure<JsonOptions>(_ => { })
                .BuildServiceProvider(),
        };
        context.Response.Body = new MemoryStream();

        await new BillingApiResult<T>(result, code, message, status).ExecuteAsync(context);

        context.Response.Body.Position = 0;
        return (
            context.Response.StatusCode,
            await JsonSerializer.DeserializeAsync<JsonElement>(
                context.Response.Body,
                cancellationToken: Xunit.TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task ExecuteAsync_MovesTheResultUnderData_ForASuccess()
    {
        var (status, body) = await ExecuteAsync(
            new Balance(Code.Success, "", 42),
            Code.Success,
            "",
            StatusCodes.Status200OK
        );

        var data = body.GetProperty("data");
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("Success", body.GetProperty("resultCode").GetString());
        Assert.Equal(42, data.GetProperty("remaining").GetInt64());
        Assert.False(data.TryGetProperty("resultCode", out _));
        Assert.False(data.TryGetProperty("message", out _));
    }

    [Fact]
    public async Task ExecuteAsync_UnwrapsTheResultsData_ForAResultHoldingOnlyData()
    {
        var (_, body) = await ExecuteAsync(
            new Listing(Code.Success, "", [1, 2]),
            Code.Success,
            "",
            StatusCodes.Status200OK
        );

        Assert.Equal(2, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteAsync_LeavesDataEmpty_ForAFailure()
    {
        var (status, body) = await ExecuteAsync(
            new Balance(Code.NoCapacityError, "No capacity", 0),
            Code.NoCapacityError,
            "No capacity",
            StatusCodes.Status409Conflict
        );

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("NoCapacityError", body.GetProperty("resultCode").GetString());
        Assert.Equal("No capacity", body.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
    }
}
