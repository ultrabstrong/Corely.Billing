using Corely.Billing.Models;
using Corely.Billing.Quota.Models;
using Corely.Billing.Registrations.Models;
using Corely.Billing.Web.IAM.Extensions;
using Microsoft.AspNetCore.Http;

namespace Corely.Billing.Web.IAM.UnitTests.Extensions;

public class ResultCodeStatusTests
{
    [Theory]
    [InlineData(QuotaReadResultCode.Success, StatusCodes.Status200OK)]
    [InlineData(QuotaReadResultCode.WrongKindError, StatusCodes.Status400BadRequest)]
    [InlineData(QuotaReadResultCode.UnauthorizedError, StatusCodes.Status403Forbidden)]
    public void StatusCode_IsTheMappedStatus_ForAQuotaReadCode(
        QuotaReadResultCode code,
        int status
    ) => Assert.Equal(status, code.StatusCode);

    [Theory]
    [InlineData(ReserveQuotaResultCode.Success, StatusCodes.Status200OK)]
    [InlineData(ReserveQuotaResultCode.ValidationError, StatusCodes.Status400BadRequest)]
    [InlineData(ReserveQuotaResultCode.UnauthorizedError, StatusCodes.Status403Forbidden)]
    [InlineData(ReserveQuotaResultCode.NoGrantAvailableError, StatusCodes.Status409Conflict)]
    [InlineData(ReserveQuotaResultCode.InsufficientQuotaError, StatusCodes.Status409Conflict)]
    [InlineData(ReserveQuotaResultCode.NotRecordedError, StatusCodes.Status409Conflict)]
    public void StatusCode_IsTheMappedStatus_ForAReserveQuotaCode(
        ReserveQuotaResultCode code,
        int status
    ) => Assert.Equal(status, code.StatusCode);

    [Theory]
    [InlineData(SettleQuotaResultCode.Success, StatusCodes.Status200OK)]
    [InlineData(SettleQuotaResultCode.UnauthorizedError, StatusCodes.Status403Forbidden)]
    [InlineData(SettleQuotaResultCode.NotRecordedError, StatusCodes.Status409Conflict)]
    public void StatusCode_IsTheMappedStatus_ForASettleQuotaCode(
        SettleQuotaResultCode code,
        int status
    ) => Assert.Equal(status, code.StatusCode);

    [Theory]
    [InlineData(RegisterResultCode.Registered, StatusCodes.Status200OK)]
    [InlineData(RegisterResultCode.ValidationError, StatusCodes.Status400BadRequest)]
    [InlineData(RegisterResultCode.WrongKindError, StatusCodes.Status400BadRequest)]
    [InlineData(RegisterResultCode.UnauthorizedError, StatusCodes.Status403Forbidden)]
    [InlineData(RegisterResultCode.AlreadyRegisteredError, StatusCodes.Status409Conflict)]
    [InlineData(RegisterResultCode.NoCapacityError, StatusCodes.Status409Conflict)]
    [InlineData(RegisterResultCode.NoActiveGrantError, StatusCodes.Status409Conflict)]
    public void StatusCode_IsTheMappedStatus_ForARegisterCode(
        RegisterResultCode code,
        int status
    ) => Assert.Equal(status, code.StatusCode);

    [Theory]
    [InlineData(ModifyResultCode.Success, StatusCodes.Status200OK)]
    [InlineData(ModifyResultCode.ValidationError, StatusCodes.Status400BadRequest)]
    [InlineData(ModifyResultCode.UnauthorizedError, StatusCodes.Status403Forbidden)]
    [InlineData(ModifyResultCode.NotFoundError, StatusCodes.Status404NotFound)]
    public void StatusCode_IsTheMappedStatus_ForAModifyCode(ModifyResultCode code, int status) =>
        Assert.Equal(status, code.StatusCode);

    [Theory]
    [InlineData(RetrieveResultCode.Success, StatusCodes.Status200OK)]
    [InlineData(RetrieveResultCode.UnauthorizedError, StatusCodes.Status403Forbidden)]
    [InlineData(RetrieveResultCode.NotFoundError, StatusCodes.Status404NotFound)]
    public void StatusCode_IsTheMappedStatus_ForARetrieveCode(
        RetrieveResultCode code,
        int status
    ) => Assert.Equal(status, code.StatusCode);
}
