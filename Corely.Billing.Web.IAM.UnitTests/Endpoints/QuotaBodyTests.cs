using System.Text.Json;
using Corely.Billing.Usage;
using Corely.Billing.Web.IAM.Endpoints;

namespace Corely.Billing.Web.IAM.UnitTests.Endpoints;

public class QuotaBodyTests
{
    private static readonly Guid _accountId = Guid.CreateVersion7();
    private static readonly UsageOperation _operation = UsageOperation.From("text_generation");
    private static readonly Guid _operationId = Guid.CreateVersion7();

    [Fact]
    public void OperationContext_IsTheOperationIdInAnApiIdempotencyScope_ForABody()
    {
        var context = new SettleQuotaBody(_operationId, "token", 1).OperationContext;

        Assert.Equal(_operationId, context.CorrelationId);
        Assert.Equal($"api:{_operationId}", context.IdempotencyScope);
    }

    [Fact]
    public void ToReserveQuotaRequest_CarriesTheAccountOperationAndUser_ForAWellFormedBody()
    {
        var userId = Guid.CreateVersion7();

        var request = new ReserveQuotaBody(
            _operationId,
            "token",
            10,
            "openai",
            new() { ["k"] = "v" }
        ).ToReserveQuotaRequest(_accountId, _operation, userId);

        Assert.NotNull(request);
        Assert.Equal(_accountId, request.AccountId);
        Assert.Equal(_operation, request.Operation);
        Assert.Equal("token", request.Unit.Value);
        Assert.Equal(10, request.Quantity);
        Assert.Equal("openai", request.Provider);
        Assert.Equal(userId, request.UserId);
        Assert.Equal("v", request.Tags!["k"]);
    }

    [Fact]
    public void ToReserveQuotaRequest_IsNull_ForAMalformedUnit() =>
        Assert.Null(
            new ReserveQuotaBody(_operationId, "Token", 10, "openai").ToReserveQuotaRequest(
                _accountId,
                _operation,
                null
            )
        );

    [Fact]
    public void ToSettleQuotaRequest_CarriesTheActualQuantity_ForAWellFormedBody()
    {
        var request = new SettleQuotaBody(_operationId, "token", 7).ToSettleQuotaRequest(
            _accountId,
            _operation
        );

        Assert.NotNull(request);
        Assert.Equal(7, request.ActualQuantity);
        Assert.Equal(_accountId, request.AccountId);
    }

    [Fact]
    public void ToSettleQuotaRequest_IsNull_ForAMalformedUnit() =>
        Assert.Null(
            new SettleQuotaBody(_operationId, "", 7).ToSettleQuotaRequest(_accountId, _operation)
        );

    [Fact]
    public void ToReleaseQuotaRequest_CarriesTheUnit_ForAWellFormedBody() =>
        Assert.Equal(
            "token",
            new ReleaseQuotaBody(_operationId, "token")
                .ToReleaseQuotaRequest(_accountId, _operation)!
                .Unit.Value
        );

    [Fact]
    public void ToReleaseQuotaRequest_IsNull_ForAMalformedUnit() =>
        Assert.Null(
            new ReleaseQuotaBody(_operationId, "to ken").ToReleaseQuotaRequest(
                _accountId,
                _operation
            )
        );

    [Fact]
    public void ToRegisterRequest_CarriesTheReferenceAndDescription_ForABodyWithBoth()
    {
        var request = new RegisterBody("seat-7", "Ana").ToRegisterRequest(_accountId, _operation);

        Assert.Equal("seat-7", request.Reference);
        Assert.Equal("Ana", request.Description);
        Assert.Equal(_operation, request.Operation);
    }

    [Fact]
    public void Deserialize_ReadsTheOperationId_ForCamelCaseJson()
    {
        var body = JsonSerializer.Deserialize<ReserveQuotaBody>(
            $$"""{"operationId":"{{_operationId}}","unit":"token","quantity":1,"provider":"p"}""",
            JsonSerializerOptions.Web
        );

        Assert.Equal(_operationId, body!.OperationId);
    }
}
