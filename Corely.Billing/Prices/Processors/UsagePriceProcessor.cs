using Corely.Billing.Grants.Enums;
using Corely.Billing.Prices.Entities;
using Corely.Billing.Prices.Enums;
using Corely.Billing.Prices.Models;
using Corely.Billing.Usage;
using Corely.Common.Extensions;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Options;

namespace Corely.Billing.Prices.Processors;

internal class UsagePriceProcessor(
    IRepo<UsagePriceEntity> priceRepo,
    IUsageVocabulary vocabulary,
    IOptions<PriceOptions> options,
    PriceSourceSelection selection
) : IUsagePriceProcessor
{
    private readonly IRepo<UsagePriceEntity> _priceRepo = priceRepo.ThrowIfNull(nameof(priceRepo));
    private readonly IUsageVocabulary _vocabulary = vocabulary.ThrowIfNull(nameof(vocabulary));
    private readonly PriceOptions _options = options.ThrowIfNull(nameof(options)).Value;

    public PriceSource Source { get; } = selection.ThrowIfNull(nameof(selection)).Source;

    public async Task<Money?> GetPriceAsync(
        UsageOperation operation,
        CancellationToken ct = default
    )
    {
        if (Source == PriceSource.Configuration)
            return _options.Prices.TryGetValue(operation.Value, out var configured)
                ? configured.ToMoney()
                : null;

        var entity = await _priceRepo.GetAsync(
            e => e.Operation == operation,
            cancellationToken: ct
        );
        return entity is null ? null : new Money(entity.Amount, entity.Currency);
    }

    public async Task<IReadOnlyList<UsagePrice>> ListPricesAsync(CancellationToken ct = default)
    {
        if (Source == PriceSource.Configuration)
            return
            [
                .. _options.Prices.Select(p => new UsagePrice(
                    UsageOperation.From(p.Key),
                    p.Value.ToMoney()
                )),
            ];

        var entities = await _priceRepo.ListAsync(cancellationToken: ct);
        return
        [
            .. entities.Select(e => new UsagePrice(e.Operation, new Money(e.Amount, e.Currency))),
        ];
    }

    public async Task<ChangeUsagePriceResult> SetPriceAsync(
        UsagePrice price,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(price);

        if (Source == PriceSource.Configuration)
            return Configured();
        if ((Problem(price.Operation) ?? price.UnitPrice.Problem()) is { } problem)
            return new ChangeUsagePriceResult(ChangeUsagePriceResultCode.ValidationError, problem);

        var entity = await _priceRepo.GetAsync(
            e => e.Operation == price.Operation,
            cancellationToken: ct
        );
        if (entity is null)
        {
            await _priceRepo.CreateAsync(
                new UsagePriceEntity
                {
                    Operation = price.Operation,
                    Amount = price.UnitPrice.Amount,
                    Currency = price.UnitPrice.Currency,
                },
                ct
            );
        }
        else
        {
            entity.Amount = price.UnitPrice.Amount;
            entity.Currency = price.UnitPrice.Currency;
            await _priceRepo.UpdateAsync(entity, ct);
        }
        return new ChangeUsagePriceResult(ChangeUsagePriceResultCode.Success, string.Empty);
    }

    public async Task<ChangeUsagePriceResult> ClearPriceAsync(
        UsageOperation operation,
        CancellationToken ct = default
    )
    {
        if (Source == PriceSource.Configuration)
            return Configured();

        var entity = await _priceRepo.GetAsync(
            e => e.Operation == operation,
            cancellationToken: ct
        );
        if (entity is not null)
            await _priceRepo.DeleteAsync(entity, ct);
        return new ChangeUsagePriceResult(ChangeUsagePriceResultCode.Success, string.Empty);
    }

    private string? Problem(UsageOperation operation) =>
        _vocabulary.Definition(operation) switch
        {
            null => $"Operation '{operation}' is not a registered usage operation.",
            { Kind: not GrantKind.Consumable } definition =>
                $"Operation '{operation}' is {definition.Kind}. Only a consumable operation has a "
                    + "price per unit.",
            _ => null,
        };

    private static ChangeUsagePriceResult Configured() =>
        new(
            ChangeUsagePriceResultCode.ConfiguredError,
            $"Prices come from configuration ({PriceOptions.NAME}), so they are changed there."
        );
}
