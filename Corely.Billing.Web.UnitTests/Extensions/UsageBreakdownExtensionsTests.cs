using Corely.Billing.Web.Components;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class UsageBreakdownExtensionsTests
{
    [Theory]
    [InlineData(UsageBreakdown.Operation, false)]
    [InlineData(UsageBreakdown.Provider, false)]
    [InlineData(UsageBreakdown.Grant, true)]
    public void ReadsGrants_IsTrueOnlyForGrant_ForEachBreakdown(UsageBreakdown by, bool expected) =>
        Assert.Equal(expected, by.ReadsGrants());
}
