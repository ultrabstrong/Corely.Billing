using Corely.Billing.Models;
using Corely.Billing.Registrations.Enums;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Registrations.Processors;
using Corely.Billing.Telemetry;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.UnitTests.Registrations.Processors;

public class RegistrationProcessorTelemetryDecoratorTests
{
    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherGrantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid RegistrationId = Guid.Parse(
        "44444444-4444-4444-4444-444444444444"
    );

    private readonly Mock<IRegistrationProcessor> _inner = new();
    private readonly Mock<IBillingTelemetry> _telemetry = new();
    private readonly RegistrationProcessorTelemetryDecorator _decorator;

    public RegistrationProcessorTelemetryDecoratorTests()
    {
        _decorator = new RegistrationProcessorTelemetryDecorator(
            _inner.Object,
            Mock.Of<ILogger<RegistrationProcessorTelemetryDecorator>>(),
            _telemetry.Object
        );
    }

    private static Registration Moved() =>
        new(
            RegistrationId,
            AccountId,
            OtherGrantId,
            TestUsage.Members,
            null,
            null,
            DateTime.UnixEpoch,
            null,
            RegistrationStatus.Active
        );

    [Theory]
    [InlineData(RegisterResultCode.Registered, BillingMetricNames.Registrations.REGISTERED)]
    [InlineData(RegisterResultCode.NoCapacityError, BillingMetricNames.Registrations.REFUSED)]
    [InlineData(
        RegisterResultCode.AlreadyRegisteredError,
        BillingMetricNames.Registrations.REFUSED
    )]
    public async Task RegisterAsync_CountsTheOutcome_ForEachResult(
        RegisterResultCode resultCode,
        string metric
    )
    {
        _inner
            .Setup(p => p.RegisterAsync(It.IsAny<RegisterRequest>(), default))
            .ReturnsAsync(new RegisterResult(resultCode, ""));

        await _decorator.RegisterAsync(new RegisterRequest(AccountId, TestUsage.Members));

        _telemetry.Verify(t => t.Increment(metric), Times.Once);
        _telemetry.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(ModifyResultCode.Success, 1)]
    [InlineData(ModifyResultCode.NotFoundError, 0)]
    public async Task ReleaseAsync_CountsOnlyARelease_ForEachResult(
        ModifyResultCode resultCode,
        int expected
    )
    {
        _inner
            .Setup(p => p.ReleaseAsync(AccountId, RegistrationId, default))
            .ReturnsAsync(new ModifyResult(resultCode, ""));

        await _decorator.ReleaseAsync(AccountId, RegistrationId);

        _telemetry.Verify(
            t => t.Increment(BillingMetricNames.Registrations.RELEASED),
            Times.Exactly(expected)
        );
    }

    [Fact]
    public async Task MoveAsync_RecordsHowManyMoved_ForAMove()
    {
        _inner
            .Setup(p => p.MoveAsync(It.IsAny<MoveRegistrationsRequest>(), default))
            .ReturnsAsync(
                new MoveRegistrationsResult(MoveRegistrationsResultCode.Success, "", [Moved()])
            );

        await _decorator.MoveAsync(new MoveRegistrationsRequest(AccountId, GrantId, OtherGrantId));

        _telemetry.Verify(t => t.Record(BillingMetricNames.Registrations.MOVED, 1), Times.Once);
    }

    [Fact]
    public async Task MoveAsync_RecordsNothing_ForAMoveThatMovedNothing()
    {
        _inner
            .Setup(p => p.MoveAsync(It.IsAny<MoveRegistrationsRequest>(), default))
            .ReturnsAsync(
                new MoveRegistrationsResult(MoveRegistrationsResultCode.NotFoundError, "", [])
            );

        await _decorator.MoveAsync(new MoveRegistrationsRequest(AccountId, GrantId, OtherGrantId));

        _telemetry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReleaseAllOnGrantListAndCount_DelegateWithoutMetrics_ForEachCall()
    {
        _inner.Setup(p => p.ReleaseAllOnGrantAsync(AccountId, GrantId, default)).ReturnsAsync(2);
        _inner
            .Setup(p => p.ListAsync(It.IsAny<ListRegistrationsRequest>(), default))
            .ReturnsAsync(
                new RetrieveListResult<Registration>(RetrieveResultCode.Success, "", null)
            );
        _inner
            .Setup(p => p.CountInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, long> { [GrantId] = 3 });

        Assert.Equal(2, await _decorator.ReleaseAllOnGrantAsync(AccountId, GrantId));
        await _decorator.ListAsync(new ListRegistrationsRequest([AccountId]));
        Assert.Equal(3, (await _decorator.CountInUseAsync([GrantId]))[GrantId]);

        _telemetry.VerifyNoOtherCalls();
    }
}
