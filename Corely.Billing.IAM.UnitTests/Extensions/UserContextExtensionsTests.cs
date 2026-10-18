using Corely.Billing.IAM.Extensions;
using Corely.IAM.Users.Models;

namespace Corely.Billing.IAM.UnitTests.Extensions;

public class UserContextExtensionsTests
{
    private static readonly Guid SignedIn = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Claimed = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public void ActingUserId_IsTheSignedInUser_ForAnyClaim() =>
        Assert.Equal(
            SignedIn,
            new UserContext(
                new User { Id = SignedIn, Username = "priya" },
                null,
                "device",
                []
            ).ActingUserId(Claimed)
        );

    [Fact]
    public void ActingUserId_IsTheClaim_ForTheSystemContext() =>
        Assert.Equal(Claimed, new UserContext(true, "device").ActingUserId(Claimed));

    [Fact]
    public void ActingUserId_IsTheClaim_ForNoContext() =>
        Assert.Equal(Claimed, ((UserContext?)null).ActingUserId(Claimed));
}
