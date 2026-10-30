using Corely.Billing.IAM;
using Corely.Billing.Web.IAM.Extensions;
using Corely.IAM.Security.Enums;

namespace Corely.Billing.Web.IAM.UnitTests.Extensions;

public class GrantActionExtensionsTests
{
    private const string GRANT = BillingResourceTypes.GRANT_RESOURCE_TYPE;
    private const string GRANT_REQUEST = BillingResourceTypes.GRANT_REQUEST_RESOURCE_TYPE;

    [Theory]
    [InlineData(GrantAction.Create, AuthAction.Create)]
    [InlineData(GrantAction.Update, AuthAction.Update)]
    [InlineData(GrantAction.Delete, AuthAction.Delete)]
    public void Permissions_AreTheSameActionOnGrant_ForGrantActions(
        GrantAction action,
        AuthAction expected
    ) => Assert.Equal([(expected, GRANT)], action.Permissions());

    [Fact]
    public void Permissions_AreCreateOnGrantRequest_ForRequest() =>
        Assert.Equal([(AuthAction.Create, GRANT_REQUEST)], GrantAction.Request.Permissions());

    [Fact]
    public void Permissions_AreUpdateOnGrantRequestThenCreateOnGrant_ForDecide() =>
        Assert.Equal(
            [(AuthAction.Update, GRANT_REQUEST), (AuthAction.Create, GRANT)],
            GrantAction.Decide.Permissions()
        );

    [Fact]
    public void Permissions_AreExecuteOnRegistration_ForManageRegistrations() =>
        Assert.Equal(
            [(AuthAction.Execute, BillingResourceTypes.REGISTRATION_RESOURCE_TYPE)],
            GrantAction.ManageRegistrations.Permissions()
        );

    [Fact]
    public void Permissions_MapsEveryMember_ForTheWholeEnum() =>
        Assert.All(Enum.GetValues<GrantAction>(), action => Assert.NotEmpty(action.Permissions()));
}
