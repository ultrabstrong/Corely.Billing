using Corely.Billing.Web.Extensions;

namespace Corely.Billing.Web.UnitTests.Extensions;

public class GuidExtensionsTests
{
    [Fact]
    public void ShortText_IsTheLastEightCharacters() =>
        Assert.Equal("89abcdef", Guid.Parse("01234567-89ab-cdef-0123-456789abcdef").ShortText());
}
