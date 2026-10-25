using Corely.Billing.Grants.Models;
using Corely.Billing.Grants.Processors;
using Corely.Billing.Models;
using Corely.Billing.Telemetry;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Grants.Processors;

public class GrantProcessorTelemetryDecoratorTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IGrantProcessor> _inner = new();
    private readonly Mock<IBillingTelemetry> _telemetry = new();
    private readonly GrantProcessorTelemetryDecorator _decorator;

    public GrantProcessorTelemetryDecoratorTests()
    {
        _decorator = new GrantProcessorTelemetryDecorator(
            _inner.Object,
            Mock.Of<ILogger<GrantProcessorTelemetryDecorator>>(),
            _telemetry.Object
        );
    }

    private static CreateGrantRequest CreateRequest() =>
        new(AccountId, TestUsage.Extraction, GrantKind.Consumable, 100, From, From.AddDays(30));

    [Fact]
    public async Task CreateGrantAsync_RecordsTheGrant_ForASuccessfulCreate()
    {
        _inner
            .Setup(p => p.CreateGrantAsync(It.IsAny<CreateGrantRequest>(), default))
            .ReturnsAsync(new CreateGrantResult(CreateGrantResultCode.Success, "", GrantId));

        await _decorator.CreateGrantAsync(CreateRequest());

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Grants.GRANT_SAVED), Times.Once);
        _telemetry.Verify(t => t.Record(BillingMetricNames.Grants.GRANT_QUANTITY, 100), Times.Once);
        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Grants.GRANT_VALIDITY_DAYS, 30),
            Times.Once
        );
    }

    [Theory]
    [InlineData(true, BillingMetricNames.Grants.GRANT_DISABLED)]
    [InlineData(false, BillingMetricNames.Grants.GRANT_ENABLED)]
    public async Task SetGrantDisabledAsync_CountsTheChange_ForEachDirection(
        bool disabled,
        string metric
    )
    {
        _inner
            .Setup(p => p.SetGrantDisabledAsync(It.IsAny<SetGrantDisabledRequest>(), default))
            .ReturnsAsync(new ModifyResult(ModifyResultCode.Success, ""));

        await _decorator.SetGrantDisabledAsync(new(AccountId, GrantId, disabled));

        _telemetry.Verify(t => t.Increment(metric), Times.Once);
    }

    [Fact]
    public async Task SetGrantDisabledAsync_CountsNothing_ForAMissingGrant()
    {
        _inner
            .Setup(p => p.SetGrantDisabledAsync(It.IsAny<SetGrantDisabledRequest>(), default))
            .ReturnsAsync(new ModifyResult(ModifyResultCode.NotFoundError, ""));

        await _decorator.SetGrantDisabledAsync(new(AccountId, GrantId, true));

        _telemetry.Verify(t => t.Increment(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreateGrantAsync_RecordsNoQuantity_ForAnUnlimitedGrant()
    {
        _inner
            .Setup(p => p.CreateGrantAsync(It.IsAny<CreateGrantRequest>(), default))
            .ReturnsAsync(new CreateGrantResult(CreateGrantResultCode.Success, "", GrantId));

        await _decorator.CreateGrantAsync(CreateRequest() with { Quantity = null });

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Grants.GRANT_SAVED), Times.Once);
        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Grants.GRANT_QUANTITY, It.IsAny<double>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateGrantAsync_RecordsNothing_ForAFailedCreate()
    {
        _inner
            .Setup(p => p.CreateGrantAsync(It.IsAny<CreateGrantRequest>(), default))
            .ReturnsAsync(
                new CreateGrantResult(CreateGrantResultCode.ValidationError, "no", Guid.Empty)
            );

        await _decorator.CreateGrantAsync(CreateRequest());

        _telemetry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateGrantAsync_RecordsTheUpdate_ForASuccessfulUpdate()
    {
        _inner
            .Setup(p => p.UpdateGrantAsync(It.IsAny<UpdateGrantRequest>(), default))
            .ReturnsAsync(new ModifyResult(ModifyResultCode.Success, ""));

        await _decorator.UpdateGrantAsync(
            new UpdateGrantRequest(AccountId, GrantId, 40, From, From.AddDays(1))
        );

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Grants.GRANT_UPDATED), Times.Once);
        _telemetry.Verify(t => t.Record(BillingMetricNames.Grants.GRANT_QUANTITY, 40), Times.Once);
    }

    [Fact]
    public async Task DeleteGrantAsync_RecordsTheDelete_ForASuccessfulDelete()
    {
        _inner
            .Setup(p => p.DeleteGrantAsync(AccountId, GrantId, default))
            .ReturnsAsync(new DeleteGrantResult(DeleteGrantResultCode.Success, ""));

        await _decorator.DeleteGrantAsync(AccountId, GrantId);

        _telemetry.Verify(t => t.Increment(BillingMetricNames.Grants.GRANT_DELETED), Times.Once);
    }

    [Fact]
    public async Task ListGrantsAsync_RecordsTheTotal_ForASuccessfulList()
    {
        _inner
            .Setup(p => p.ListGrantsAsync(It.IsAny<ListGrantsRequest>(), null, default))
            .ReturnsAsync(
                new RetrieveListResult<Grant>(
                    RetrieveResultCode.Success,
                    "",
                    PagedResult<Grant>.Create([new Grant()], 7, 0, 1)
                )
            );

        await _decorator.ListGrantsAsync(new ListGrantsRequest([AccountId]));

        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Grants.GRANT_TOTAL_COUNT, 7),
            Times.Once
        );
    }

    [Fact]
    public async Task ListActiveGrantsAsync_RecordsTheCount_ForAnyResult()
    {
        _inner
            .Setup(p =>
                p.ListActiveGrantsAsync(
                    AccountId,
                    TestUsage.Extraction,
                    GrantKind.Consumable,
                    From,
                    default
                )
            )
            .ReturnsAsync([new Grant(), new Grant()]);

        await _decorator.ListActiveGrantsAsync(
            AccountId,
            TestUsage.Extraction,
            GrantKind.Consumable,
            From
        );

        _telemetry.Verify(
            t => t.Record(BillingMetricNames.Grants.GRANT_ACTIVE_COUNT, 2),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateGrantAsync_Rethrows_ForAFaultInTheInnerProcessor()
    {
        _inner
            .Setup(p => p.CreateGrantAsync(It.IsAny<CreateGrantRequest>(), default))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _decorator.CreateGrantAsync(CreateRequest())
        );
        _telemetry.VerifyNoOtherCalls();
    }
}
