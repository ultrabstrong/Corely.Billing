using Corely.Billing.Web.Components;
using Microsoft.AspNetCore.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class HostOwnedPathsTests
{
    [Fact]
    public void Assembly_HasNoRoutedBlazorPage_ForAnyType()
    {
        var routed = typeof(GrantList)
            .Assembly.GetTypes()
            .Where(t => t.IsDefined(typeof(RouteAttribute), false))
            .Select(t => t.FullName);

        Assert.Empty(routed);
    }
}
