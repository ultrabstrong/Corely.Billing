using Corely.Billing.Consumption.Models;
using Corely.Billing.Quota.Mappers;
using Corely.Billing.Quota.Models;

namespace Corely.Billing.UnitTests.Quota.Mappers;

public class ResolveConsumptionResultExtensionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToSettleQuotaResult_CarriesTheQuantityAndOverdraft_ForASuccess(bool overdrawn)
    {
        var result = new ResolveConsumptionResult(
            ResolveConsumptionResultCode.Success,
            "",
            SettledQuantity: 42,
            RowCount: 2
        ).ToSettleQuotaResult(overdrawn);

        Assert.Equal(
            new SettleQuotaResult(SettleQuotaResultCode.Success, "", 42, overdrawn),
            result
        );
    }

    [Fact]
    public void ToSettleQuotaResult_ReportsNotRecordedWithTheReason_ForAFailure()
    {
        var result = new ResolveConsumptionResult(
            ResolveConsumptionResultCode.NotRecordedError,
            "No ambient operation context."
        ).ToSettleQuotaResult(overdrawn: false);

        Assert.Equal(SettleQuotaResultCode.NotRecordedError, result.ResultCode);
        Assert.Equal("No ambient operation context.", result.Message);
        Assert.Null(result.RemainingRatio);
    }
}
