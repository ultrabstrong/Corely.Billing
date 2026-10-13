using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class CopyableIdTests : BunitContext
{
    private readonly Guid _id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    [Fact]
    public void Short_ShowsTheShortForm_AndKeepsTheFullIdInTheTooltip()
    {
        var cut = Render<CopyableId>(p => p.Add(x => x.Value, _id).Add(x => x.Short, true));

        Assert.Equal("89abcdef", cut.Find(".cbw-id-text").TextContent);
        Assert.Equal(_id.ToString(), cut.Find(".cbw-id-text").GetAttribute("title"));
    }

    [Fact]
    public void Copy_WritesTheFullIdToTheClipboard_AndShowsItWasCopied()
    {
        var copy = JSInterop.SetupVoid("navigator.clipboard.writeText", _id.ToString());
        copy.SetVoidResult();
        var cut = Render<CopyableId>(p => p.Add(x => x.Value, _id).Add(x => x.Short, true));

        cut.Find("button.cbw-copy-btn").Click();

        copy.VerifyInvoke("navigator.clipboard.writeText");
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("button.cbw-copy-btn .bi-check2")));
    }
}
