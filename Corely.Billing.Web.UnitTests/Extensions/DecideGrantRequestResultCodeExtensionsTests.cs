using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class DecideGrantRequestResultCodeExtensionsTests
{
    [Theory]
    [InlineData(DecideGrantRequestResultCode.Success, "", null)]
    [InlineData(
        DecideGrantRequestResultCode.UnauthorizedError,
        "denied",
        "You are not allowed to approve grant requests."
    )]
    [InlineData(DecideGrantRequestResultCode.NotFoundError, "", "That request no longer exists.")]
    [InlineData(
        DecideGrantRequestResultCode.AlreadyDecidedError,
        "",
        "Someone else already decided that request."
    )]
    [InlineData(
        DecideGrantRequestResultCode.ValidationError,
        "Valid To must be after Valid From.",
        "Valid To must be after Valid From."
    )]
    [InlineData(
        DecideGrantRequestResultCode.ValidationError,
        " ",
        "Could not approve the request."
    )]
    public void ErrorMessage_ExplainsTheOutcome_ForEachCode(
        DecideGrantRequestResultCode code,
        string message,
        string? expected
    ) => Assert.Equal(expected, code.ErrorMessage("approve", message));
}
