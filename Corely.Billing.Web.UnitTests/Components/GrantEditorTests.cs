using Corely.Billing.Grants.Models;
using Corely.Billing.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class GrantEditorTests : BillingWebTestContext
{
    private CreateGrantRequest? _created;

    public GrantEditorTests()
    {
        Grants
            .Setup(g =>
                g.CreateGrantAsync(It.IsAny<CreateGrantRequest>(), It.IsAny<CancellationToken>())
            )
            .Callback<CreateGrantRequest, CancellationToken>((r, _) => _created = r)
            .ReturnsAsync(
                new CreateGrantResult(CreateGrantResultCode.Success, "", Guid.CreateVersion7())
            );
    }

    private IRenderedComponent<GrantEditor> Render(Guid? grantId = null, bool canManage = true) =>
        Render<GrantEditor>(p =>
            p.Add(c => c.AccountId, AccountId)
                .Add(c => c.GrantId, grantId)
                .Add(c => c.CanManage, canManage)
        );

    [Fact]
    public void Save_RefusesWithoutCallingTheService_ForAWindowThatEndsBeforeItStarts()
    {
        var editor = Render();
        editor.Find("#cbw-to").Change(Now.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss"));

        editor.Find("form").Submit();

        Assert.Equal(
            "Valid to must be after valid from.",
            editor.Find(".alert-danger").TextContent
        );
        Assert.Null(_created);
    }

    [Fact]
    public void Save_CreatesAGrantWithNoQuantity_ForUnlimited()
    {
        var editor = Render();
        editor.Find("#cbw-quantity").Change("500");
        editor.Find("#cbw-unlimited").Change(true);

        editor.Find("form").Submit();

        Assert.NotNull(_created);
        Assert.Null(_created!.Quantity);
        Assert.Equal(AccountId, _created.AccountId);
    }

    [Fact]
    public void Save_ShowsTheServiceMessage_ForARejectedGrant()
    {
        Grants
            .Setup(g =>
                g.CreateGrantAsync(It.IsAny<CreateGrantRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new CreateGrantResult(
                    CreateGrantResultCode.ValidationError,
                    "Unit 'x' is not registered",
                    Guid.Empty
                )
            );
        var editor = Render();

        editor.Find("form").Submit();

        Assert.Equal("Unit 'x' is not registered", editor.Find(".alert-danger").TextContent);
    }

    [Fact]
    public void Render_LocksTheOperation_ForAnExistingGrant()
    {
        var grant = Grant(250);
        Grants
            .Setup(g => g.GetGrantAsync(AccountId, grant.GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrieveSingleResult<Grant>(RetrieveResultCode.Success, "", grant));

        var editor = Render(grant.GrantId);

        Assert.True(editor.Find("#cbw-operation").HasAttribute("disabled"));
        Assert.Equal("250", editor.Find("#cbw-quantity").GetAttribute("value"));
    }

    [Fact]
    public void Render_OffersNoSave_ForAHostThatCannotManage()
    {
        var editor = Render(canManage: false);

        Assert.Empty(editor.FindAll("button[type=submit]"));
        Assert.Contains("can view this grant but not change it", editor.Markup);
    }
}
