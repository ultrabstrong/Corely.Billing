using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Models;
using Corely.Billing.Prices.Models;
using Corely.Billing.Serialization;
using Corely.Billing.Usage;

namespace Corely.Billing.Grants.Mappers;

internal static class GrantMapper
{
    extension(CreateGrantRequest request)
    {
        public Grant ToGrant(UsageUnit? declaredUnit, Money? currentPrice) =>
            new()
            {
                GrantId = Guid.CreateVersion7(),
                AccountId = request.AccountId,
                Operation = request.Operation,
                Kind = request.Kind,
                Unit = request.Unit ?? declaredUnit,
                Quantity = request.Quantity,
                ValidFromUtc = request.ValidFromUtc,
                ValidToUtc = request.ValidToUtc,
                Tags = request.Tags,
                UnitPrice = request.UnitPrice ?? currentPrice,
            };
    }

    extension(Grant grant)
    {
        public GrantEntity ToEntity() =>
            new()
            {
                GrantId = grant.GrantId,
                AccountId = grant.AccountId,
                Operation = grant.Operation,
                Kind = grant.Kind,
                Unit = grant.Unit,
                Quantity = grant.Quantity,
                ValidFromUtc = grant.ValidFromUtc,
                ValidToUtc = grant.ValidToUtc,
                Disabled = grant.Disabled,
                TagsJson = TagSerializer.Serialize(grant.Tags),
                UnitPriceAmount = grant.UnitPrice?.Amount,
                UnitPriceCurrency = grant.UnitPrice?.Currency,
            };
    }

    extension(GrantEntity entity)
    {
        public Grant ToModel() =>
            new()
            {
                GrantId = entity.GrantId,
                AccountId = entity.AccountId,
                Operation = entity.Operation,
                Kind = entity.Kind,
                Unit = entity.Unit,
                Quantity = entity.Quantity,
                ValidFromUtc = entity.ValidFromUtc,
                ValidToUtc = entity.ValidToUtc,
                Disabled = entity.Disabled,
                Tags = TagSerializer.Deserialize(entity.TagsJson),
                UnitPrice = entity.UnitPrice(),
            };

        public Money? UnitPrice() =>
            entity is { UnitPriceAmount: { } amount, UnitPriceCurrency: { } currency }
                ? new Money(amount, currency)
                : null;
    }

    extension(UpdateGrantRequest request)
    {
        public Grant ApplyTo(GrantEntity entity) =>
            new()
            {
                GrantId = entity.GrantId,
                AccountId = entity.AccountId,
                Operation = entity.Operation,
                Kind = entity.Kind,
                Unit = entity.Unit,
                Quantity = request.Quantity,
                ValidFromUtc = request.ValidFromUtc,
                ValidToUtc = request.ValidToUtc,
                Disabled = entity.Disabled,
                Tags = request.Tags,
                UnitPrice = request.UnitPrice ?? entity.UnitPrice(),
            };
    }
}
