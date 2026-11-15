using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.UnitTests.Components;

public class PriceSettingsTests : BillingWebTestContext
{
    private UsagePrice? _set;

    public PriceSettingsTests()
    {
        Prices.Setup(p => p.Source).Returns(PriceSource.Platform);
        Prices
            .Setup(p => p.SetPriceAsync(It.IsAny<UsagePrice>(), It.IsAny<CancellationToken>()))
            .Callback<UsagePrice, CancellationToken>((price, _) => _set = price)
            .ReturnsAsync(new ChangeUsagePriceResult(ChangeUsagePriceResultCode.Success, ""));
    }

    private IRenderedComponent<PriceSettings> Render(bool editable = true) =>
        Render<PriceSettings>(p => p.Add(c => c.Editable, editable));

    [Fact]
    public void Render_ListsOnlyConsumableOperations_ForAMixedVocabulary()
    {
        var rows = Render().FindAll("[data-price-operation]");

        Assert.Equal([Extraction.Value], rows.Select(r => r.GetAttribute("data-price-operation")));
    }

    [Fact]
    public void Save_SetsTheEnteredPriceInDollars_ForAPlatformPricedHost()
    {
        var settings = Render();
        settings.Find($"#cbw-price-{Extraction.Value}").Change("0.001");

        settings.Find("[data-price-operation] .btn-primary").Click();

        Assert.Equal(new UsagePrice(Extraction, Money.Usd(0.001m)), _set);
    }

    [Fact]
    public void Render_ShowsThePriceReadOnly_ForPricesFromConfiguration()
    {
        Prices.Setup(p => p.Source).Returns(PriceSource.Configuration);
        HavePrices(new UsagePrice(Extraction, Money.Usd(0.001m)));

        var settings = Render();

        Assert.Empty(settings.FindAll("input"));
        Assert.Equal("$0.001", settings.Find($"#cbw-price-{Extraction.Value}").TextContent);
        Assert.Contains(PriceOptions.NAME, settings.Markup);
    }
}
