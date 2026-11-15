using Corely.Billing.Prices.Entities;
using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Prices.Processors;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Options;

namespace Corely.Billing.UnitTests.Prices.Processors;

public class UsagePriceProcessorTests
{
    private readonly ServiceFactory _serviceFactory = new();

    private UsagePriceProcessor Processor(PriceSource source, PriceOptions? options = null) =>
        new(
            _serviceFactory.GetRequiredService<IRepo<UsagePriceEntity>>(),
            TestUsage.Vocabulary,
            Options.Create(options ?? new PriceOptions()),
            new PriceSourceSelection(source)
        );

    private static PriceOptions Configured(decimal amount) =>
        new()
        {
            Prices = new() { [TestUsage.Spend.Value] = new ConfiguredPrice { Amount = amount } },
        };

    [Fact]
    public async Task GetPriceAsync_ReadsConfiguration_ForTheConfigurationSource()
    {
        var price = await Processor(PriceSource.Configuration, Configured(0.001m))
            .GetPriceAsync(TestUsage.Spend);

        Assert.Equal(Money.Usd(0.001m), price);
    }

    [Fact]
    public async Task GetPriceAsync_IgnoresConfiguration_ForThePlatformSource()
    {
        var price = await Processor(PriceSource.Platform, Configured(0.001m))
            .GetPriceAsync(TestUsage.Spend);

        Assert.Null(price);
    }

    [Fact]
    public async Task SetPriceAsync_RefusesAndWritesNothing_ForTheConfigurationSource()
    {
        var processor = Processor(PriceSource.Configuration);

        var result = await processor.SetPriceAsync(
            new UsagePrice(TestUsage.Spend, Money.Usd(0.001m))
        );

        Assert.Equal(ChangeUsagePriceResultCode.ConfiguredError, result.ResultCode);
        Assert.Empty(
            await _serviceFactory.GetRequiredService<IRepo<UsagePriceEntity>>().ListAsync(_ => true)
        );
    }

    [Fact]
    public async Task SetPriceAsync_ReplacesThePrice_ForThePlatformSource()
    {
        var processor = Processor(PriceSource.Platform);

        await processor.SetPriceAsync(new UsagePrice(TestUsage.Spend, Money.Usd(0.001m)));
        await processor.SetPriceAsync(new UsagePrice(TestUsage.Spend, Money.Usd(0.002m)));

        Assert.Equal(Money.Usd(0.002m), await processor.GetPriceAsync(TestUsage.Spend));
        Assert.Single(await processor.ListPricesAsync());
    }

    [Fact]
    public async Task ClearPriceAsync_LeavesTheOperationUnpriced_ForThePlatformSource()
    {
        var processor = Processor(PriceSource.Platform);
        await processor.SetPriceAsync(new UsagePrice(TestUsage.Spend, Money.Usd(0.001m)));

        await processor.ClearPriceAsync(TestUsage.Spend);

        Assert.Null(await processor.GetPriceAsync(TestUsage.Spend));
    }

    [Fact]
    public async Task SetPriceAsync_Refuses_ForANonConsumableOperation()
    {
        var result = await Processor(PriceSource.Platform)
            .SetPriceAsync(new UsagePrice(TestUsage.Members, Money.Usd(5)));

        Assert.Equal(ChangeUsagePriceResultCode.ValidationError, result.ResultCode);
    }

    [Fact]
    public async Task SetPriceAsync_Refuses_ForAPriceFinerThanItsDecimals()
    {
        var result = await Processor(PriceSource.Platform)
            .SetPriceAsync(new UsagePrice(TestUsage.Spend, Money.Usd(0.0000001m)));

        Assert.Equal(ChangeUsagePriceResultCode.ValidationError, result.ResultCode);
    }
}
