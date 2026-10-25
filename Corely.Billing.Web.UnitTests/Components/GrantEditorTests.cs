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

    [Fact]
    public void ToggleDisabled_DisablesTheGrantAndSaysSo_ForAnEnabledGrant()
    {
        var grant = HaveGrant(250);
        Grants
            .Setup(g =>
                g.SetGrantDisabledAsync(
                    It.IsAny<SetGrantDisabledRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new ModifyResult(ModifyResultCode.Success, ""));
        var editor = Render(grant.GrantId);

        editor.Find(".cbw-toggle-disabled").Click();

        Grants.Verify(
            g =>
                g.SetGrantDisabledAsync(
                    new SetGrantDisabledRequest(AccountId, grant.GrantId, true),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        Assert.Equal("Enable grant", editor.Find(".cbw-toggle-disabled").TextContent.Trim());
        Assert.Contains("This grant is disabled.", editor.Markup);
    }

    [Fact]
    public void Render_OffersNoToggle_ForAnExistingGrantWithoutUpdate()
    {
        var grant = HaveGrant(250);
        ActionGate.Deny(GrantAction.Update);

        var editor = Render(grant.GrantId);

        Assert.Empty(editor.FindAll(".cbw-toggle-disabled"));
    }

    [Fact]
    public void Render_OffersNoToggle_ForANewGrant() =>
        Assert.Empty(Render().FindAll(".cbw-toggle-disabled"));

    private Grant HaveGrant(long quantity)
    {
        var grant = Grant(quantity);
        Grants
            .Setup(g => g.GetGrantAsync(AccountId, grant.GrantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrieveSingleResult<Grant>(RetrieveResultCode.Success, "", grant));
        return grant;
    }

    [Fact]
    public void Render_OffersOnlyDeclaredOperationsGroupedByKindAndNoUnit_ForANewGrant()
    {
        var editor = Render();

        Assert.Empty(editor.FindAll("#cbw-unit"));
        Assert.Equal(
            ["Balance", "Capacity", "Allowance", "Feature"],
            editor.FindAll("#cbw-operation optgroup").Select(g => g.GetAttribute("label"))
        );
        Assert.Equal(4, editor.FindAll("#cbw-operation option").Count);
        Assert.Equal("pages", editor.Find("[data-unit]").TextContent);
    }

    [Fact]
    public void Save_CreatesAFeatureWithNoQuantityOrUnit_ForAFeatureOperation()
    {
        var editor = Render();
        editor.Find("#cbw-operation").Change(Sftp.Value);

        Assert.Empty(editor.FindAll("#cbw-quantity"));
        editor.Find("form").Submit();

        Assert.NotNull(_created);
        Assert.Equal(GrantKind.Feature, _created!.Kind);
        Assert.Null(_created.Quantity);
        Assert.Null(_created.Unit);
    }

    [Fact]
    public void Save_CreatesAnAllowanceInTheDeclaredKind_ForAnAllowanceOperation()
    {
        var editor = Render();
        editor.Find("#cbw-operation").Change(Retention.Value);
        editor.Find("#cbw-quantity").Change("60");

        Assert.Equal("Amount added", editor.Find("label[for=cbw-quantity]").TextContent);
        Assert.Empty(editor.FindAll("#cbw-never-expires"));
        editor.Find("form").Submit();

        Assert.Equal(GrantKind.Allowance, _created!.Kind);
        Assert.Equal(60, _created.Quantity);
    }

    [Fact]
    public void Save_CreatesAGrantThatNeverExpires_ForTheNeverExpiresSwitch()
    {
        var editor = Render();
        editor.Find("#cbw-quantity").Change("500");
        editor.Find("#cbw-never-expires").Change(true);

        Assert.Empty(editor.FindAll("#cbw-to"));
        editor.Find("form").Submit();

        Assert.Equal(
            Corely.Billing.Grants.Constants.GrantConstants.NEVER_EXPIRES_UTC,
            _created!.ValidToUtc
        );
    }
}
