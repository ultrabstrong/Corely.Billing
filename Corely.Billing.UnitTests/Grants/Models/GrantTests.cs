using Corely.Billing;
using Corely.Billing.Grants.Models;

namespace Corely.Billing.UnitTests.Grants.Models;

public class GrantTests
{
    private static readonly Guid TestAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_Succeeds_ForValidInput()
    {
        var ok = Grant.Create(
            accountId: TestAccountId,
            quantity: Grant.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.Extraction,
            validFromUtc: DateTime.UtcNow,
            validToUtc: DateTime.UtcNow.AddMonths(1),
            grantId: Guid.CreateVersion7(),
            tags: new Dictionary<string, string> { ["contractId"] = "C-1" }
        );

        Assert.True(ok.IsSuccess);
        Assert.NotNull(ok.Value);
        Assert.Null(ok.Message);
        Assert.Multiple(() =>
        {
            Assert.Equal(TestAccountId, ok.Value!.AccountId);
            Assert.Equal(Grant.QUANTITY_MIN_VALUE, ok.Value.Quantity);
            Assert.Equal(TestUsage.Page, ok.Value.Unit);
            Assert.Equal(TestUsage.Extraction, ok.Value.Operation);
            Assert.NotNull(ok.Value.Tags);
            Assert.True(ok.Value.Tags!.ContainsKey("contractId"));
            Assert.Equal("C-1", ok.Value.Tags["contractId"]);
        });
    }

    [Fact]
    public void Create_Fails_ForEmptyAccountId()
    {
        var ok = Grant.Create(
            accountId: Guid.Empty,
            quantity: Grant.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.Other,
            validFromUtc: DateTime.UtcNow,
            validToUtc: DateTime.UtcNow.AddDays(1),
            grantId: Guid.CreateVersion7()
        );
        Assert.False(ok.IsSuccess);
        Assert.Null(ok.Value);
        Assert.NotNull(ok.Message);
    }

    [Fact]
    public void Create_Fails_ForQuantityTooLow()
    {
        var ok = Grant.Create(
            accountId: TestAccountId,
            quantity: Grant.QUANTITY_MIN_VALUE - 1,
            unit: TestUsage.Page,
            operation: TestUsage.Other,
            validFromUtc: DateTime.UtcNow,
            validToUtc: DateTime.UtcNow.AddDays(1),
            grantId: Guid.CreateVersion7()
        );
        Assert.False(ok.IsSuccess);
        Assert.Null(ok.Value);
        Assert.NotNull(ok.Message);
    }

    [Fact]
    public void Create_Fails_ForEmptyGrantId()
    {
        var ok = Grant.Create(
            accountId: TestAccountId,
            quantity: Grant.QUANTITY_MIN_VALUE,
            unit: TestUsage.Page,
            operation: TestUsage.Other,
            validFromUtc: DateTime.UtcNow,
            validToUtc: DateTime.UtcNow.AddDays(1),
            grantId: Guid.Empty
        );

        Assert.False(ok.IsSuccess);
        Assert.Null(ok.Value);
        Assert.NotNull(ok.Message);
    }
}
