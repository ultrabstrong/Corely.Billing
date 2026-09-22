using Corely.Billing;
using Corely.Billing.Consumption.Models;

namespace Corely.Billing.UnitTests.Consumption.Models;

public class ConsumptionEventTests
{
    private static readonly Guid TestGrantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TestAccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TestUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TestCorrelationId = Guid.Parse(
        "44444444-4444-4444-4444-444444444444"
    );

    [Fact]
    public void Create_GeneratesVersion7Id_ForOmittedConsumptionId()
    {
        // The id is generated here, not by the provider. ConsumptionEvents carried
        // ValueGeneratedOnAdd, which had EF fill it with a GUID ordered for SQL Server's
        // uniqueidentifier comparison -- sequencing that means nothing on Postgres or MySQL. A v7
        // is sequential in plain byte order, so it survives a provider change.
        var result = ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: ConsumptionEvent.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.NoOp,
            provider: "prov",
            utcTimestamp: DateTime.UtcNow,
            grantId: TestGrantId
        );

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.ConsumptionId);
        Assert.Equal(7, result.Value.ConsumptionId.Version);
    }

    [Fact]
    public void Create_Succeeds_ForValidMinimalInput()
    {
        var ok = ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: ConsumptionEvent.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            provider: "prov",
            utcTimestamp: DateTime.UtcNow,
            grantId: TestGrantId
        );

        Assert.True(ok.IsSuccess);
        Assert.NotNull(ok.Value);
        Assert.Null(ok.Message);
        Assert.Multiple(() =>
        {
            Assert.Equal(TestAccountId, ok.Value!.AccountId);
            Assert.Equal(ConsumptionEvent.QUANTITY_MIN_VALUE, ok.Value!.Quantity);
            Assert.Equal(TestUsage.Page, ok.Value!.Unit);
            Assert.Equal(TestUsage.Extraction, ok.Value!.Operation);
            Assert.Equal("prov", ok.Value!.Provider);
            Assert.Equal(TestGrantId, ok.Value!.GrantId);
        });
    }

    [Fact]
    public void Create_Fails_ForEmptyAccountId()
    {
        var ok = ConsumptionEvent.Create(
            accountId: Guid.Empty,
            quantity: ConsumptionEvent.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.NoOp,
            provider: "prov",
            utcTimestamp: DateTime.UtcNow,
            grantId: TestGrantId
        );

        Assert.False(ok.IsSuccess);
        Assert.Null(ok.Value);
        Assert.NotNull(ok.Message);
    }

    [Fact]
    public void Create_Fails_ForQuantityTooSmall()
    {
        var ok = ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: ConsumptionEvent.QUANTITY_MIN_VALUE - 1,
            unit: TestUsage.Page,
            operation: TestUsage.NoOp,
            provider: "prov",
            utcTimestamp: DateTime.UtcNow,
            grantId: TestGrantId
        );

        Assert.False(ok.IsSuccess);
        Assert.Null(ok.Value);
        Assert.NotNull(ok.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_Fails_ForEmptyProvider(string invalid)
    {
        var ok = ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: ConsumptionEvent.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.NoOp,
            provider: invalid,
            utcTimestamp: DateTime.UtcNow,
            grantId: TestGrantId
        );
        Assert.False(ok.IsSuccess);
        Assert.Null(ok.Value);
        Assert.NotNull(ok.Message);
    }

    [Fact]
    public void Create_Fails_ForProviderTooLong()
    {
        var longProvider = new string('p', ConsumptionEvent.PROVIDER_MAX_LENGTH + 1);
        var ok = ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: ConsumptionEvent.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.NoOp,
            provider: longProvider,
            utcTimestamp: DateTime.UtcNow,
            grantId: TestGrantId
        );
        Assert.False(ok.IsSuccess);
        Assert.Null(ok.Value);
        Assert.NotNull(ok.Message);
    }

    [Fact]
    public void Create_Fails_ForEmptyGrantId()
    {
        var ok = ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: ConsumptionEvent.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.NoOp,
            provider: "prov",
            utcTimestamp: DateTime.UtcNow,
            grantId: Guid.Empty
        );
        Assert.False(ok.IsSuccess);
        Assert.Null(ok.Value);
        Assert.NotNull(ok.Message);
    }

    [Fact]
    public void Create_LeavesTheOperationIdentityUnset_ForAnyInputs()
    {
        // Create cannot be given a correlation id or an idempotency key at all. Both belong to the
        // ambient operation, and a caller able to mint them per attempt is the double-charge.
        var ok = MakeEvent();

        Assert.Multiple(() =>
        {
            Assert.Equal(Guid.Empty, ok.Value!.CorrelationId);
            Assert.Empty(ok.Value.IdempotencyKey);
        });
    }

    [Fact]
    public void Stamp_AppliesTheOperationIdentity_ForAnUnstampedEvent()
    {
        var stamped = MakeEvent().Value!.Stamp(TestCorrelationId, "key-a", null, null);

        Assert.Multiple(() =>
        {
            Assert.Equal(TestCorrelationId, stamped.CorrelationId);
            Assert.Equal("key-a", stamped.IdempotencyKey);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Stamp_Throws_ForABlankIdempotencyKey(string key) =>
        Assert.Throws<ArgumentException>(() =>
            MakeEvent().Value!.Stamp(TestCorrelationId, key, null, null)
        );

    [Fact]
    public void Stamp_Throws_ForAnEmptyCorrelationId() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MakeEvent().Value!.Stamp(Guid.Empty, "key-a", null, null)
        );

    private static CreateResult<ConsumptionEvent> MakeEvent() =>
        ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: ConsumptionEvent.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.NoOp,
            provider: "prov",
            utcTimestamp: DateTime.UtcNow,
            grantId: TestGrantId
        );

    [Fact]
    public void Create_Succeeds_ForUserIdSet()
    {
        var ok = ConsumptionEvent.Create(
            accountId: TestAccountId,
            quantity: ConsumptionEvent.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.NoOp,
            provider: "prov",
            utcTimestamp: DateTime.UtcNow,
            grantId: TestGrantId,
            userId: TestUserId
        );
        Assert.True(ok.IsSuccess);
        Assert.NotNull(ok.Value);
        Assert.Equal(TestUserId, ok.Value!.UserId);
    }
}
