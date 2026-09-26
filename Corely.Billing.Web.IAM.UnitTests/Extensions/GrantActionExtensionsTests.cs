using Corely.Billing.Web.IAM.Extensions;
using Corely.IAM.Security.Constants;

namespace Corely.Billing.Web.IAM.UnitTests.Extensions;

public class GrantActionExtensionsTests
{
    [Theory]
    [InlineData(GrantAction.Create, AuthAction.Create)]
    [InlineData(GrantAction.Update, AuthAction.Update)]
    [InlineData(GrantAction.Delete, AuthAction.Delete)]
    public void ToAuthAction_MapsByName_ForEachAction(GrantAction action, AuthAction expected) =>
        Assert.Equal(expected, action.ToAuthAction());

    [Fact]
    public void ToAuthAction_MapsEveryMember_ForTheWholeEnum() =>
        Assert.All(
            Enum.GetValues<GrantAction>(),
            action => Assert.Equal(action.ToString(), action.ToAuthAction().ToString())
        );
}
