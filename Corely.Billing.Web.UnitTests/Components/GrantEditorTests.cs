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

    private IRenderedComponent<GrantEditor> Render(Guid? grantId = null) =>
        Render<GrantEditor>(p => p.Add(c => c.AccountId, AccountId).Add(c => c.GrantId, grantId));

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
    public void Render_ShowsNoForm_ForANewGrantWithoutCreate()
    {
        ActionGate.Deny(GrantAction.Create);

        var editor = Render();

        Assert.Empty(editor.FindAll("form"));
        Assert.Contains("You are not allowed to create grants.", editor.Markup);
        Assert.Equal([(GrantAction.Create, (Guid?)null)], ActionGate.Calls);
    }

    [Fact]
    public void Render_ShowsTheGrantReadOnly_ForAnExistingGrantWithoutUpdate()
    {
        var grant = HaveGrant(250);
        ActionGate.Deny(GrantAction.Update);

        var editor = Render(grant.GrantId);

        Assert.True(editor.Find("fieldset").HasAttribute("disabled"));
        Assert.Empty(editor.FindAll("button[type=submit]"));
        Assert.Equal("250", editor.Find("#cbw-quantity").GetAttribute("value"));
        Assert.Contains("can view this grant but not change it", editor.Markup);
        Assert.Equal([(GrantAction.Update, (Guid?)grant.GrantId)], ActionGate.Calls);
    }

    private Grant HaveGrant(long quantity)
    {
        var grant = Grant(quantity);
        Grants
            .Setup(g => g.GetGrantAsync(AccountId, grant.GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrieveSingleResult<Grant>(RetrieveResultCode.Success, "", grant));
        return grant;
    }
}
