using Corely.Billing.Models;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class RetrieveResultCodeExtensionsTests
{
    [Theory]
    [InlineData(
        RetrieveResultCode.UnauthorizedError,
        "denied",
        "You are not allowed to view usage."
    )]
    [InlineData(RetrieveResultCode.NotFoundError, "Grant not found", "Grant not found")]
    [InlineData(RetrieveResultCode.NotFoundError, " ", "Could not view usage.")]
    public void ErrorMessage_ExplainsTheFailure_ForEachCode(
        RetrieveResultCode code,
        string message,
        string expected
    ) => Assert.Equal(expected, code.ErrorMessage("view usage", message));
}
