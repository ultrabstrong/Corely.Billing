using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Usage;
using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class GrantRequestExtensionsTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly UsageUnit Page = UsageUnit.From("page");

    private static readonly IUsageVocabulary Vocabulary = Mock.Of<IUsageVocabulary>(v =>
        v.DisplayName(Page) == "page"
    );

    private static GrantRequest Request(long quantity, DateTime requestedUtc) =>
        new()
        {
            Unit = Page,
            Quantity = quantity,
            RequestedUtc = requestedUtc,
        };

    [Theory]
    [InlineData(1, "1 page")]
    [InlineData(5_000, "5,000 pages")]
    public void Asked_CountsInTheUnitsDisplayName_ForEachQuantity(long quantity, string expected) =>
        Assert.Equal(expected, Request(quantity, Now).Asked(Vocabulary));

    [Theory]
    [InlineData(0, "Requested today")]
    [InlineData(1, "Requested yesterday")]
    [InlineData(3, "Requested 3 days ago")]
    public void RequestedText_SaysHowLongAgo_ForEachAge(int daysAgo, string expected) =>
        Assert.Equal(expected, Request(1, Now.AddDays(-daysAgo)).RequestedText(Now));
}
