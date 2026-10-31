using System.Text.Json;
using System.Text.Json.Nodes;
using Corely.IAM.Web.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Corely.Billing.Web.IAM.Endpoints;

internal sealed class BillingApiResult<TResult>(
    TResult result,
    Enum resultCode,
    string? message,
    int statusCode
) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var options = httpContext
            .RequestServices.GetRequiredService<IOptions<JsonOptions>>()
            .Value.SerializerOptions;

        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(
            new ApiResponse<JsonNode>(
                resultCode.ToString(),
                message,
                statusCode < StatusCodes.Status400BadRequest ? Payload(options) : null
            ),
            options
        );
    }

    private JsonNode Payload(JsonSerializerOptions options)
    {
        var payload = JsonSerializer.SerializeToNode(result, options)!.AsObject();
        payload.Remove(Named("ResultCode", options));
        payload.Remove(Named("Message", options));

        return
            payload.TryGetPropertyValue(Named("Data", options), out var data)
            && data is not null
            && payload.Count == 1
            ? data.DeepClone()
            : payload;
    }

    private static string Named(string property, JsonSerializerOptions options) =>
        options.PropertyNamingPolicy?.ConvertName(property) ?? property;
}
