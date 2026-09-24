using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Models;
using Corely.Billing.Serialization;

namespace Corely.Billing.Grants.Mappers;

internal static class GrantMapper
{
    extension(CreateGrantRequest request)
    {
        public Grant ToGrant() =>
            new()
            {
                GrantId = Guid.CreateVersion7(),
                AccountId = request.AccountId,
                Operation = request.Operation,
                Unit = request.Unit,
                Quantity = request.Quantity,
                ValidFromUtc = request.ValidFromUtc,
                ValidToUtc = request.ValidToUtc,
                Tags = request.Tags,
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
                Unit = grant.Unit,
                Quantity = grant.Quantity,
                ValidFromUtc = grant.ValidFromUtc,
                ValidToUtc = grant.ValidToUtc,
                TagsJson = TagSerializer.Serialize(grant.Tags),
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
                Unit = entity.Unit,
                Quantity = entity.Quantity,
                ValidFromUtc = entity.ValidFromUtc,
                ValidToUtc = entity.ValidToUtc,
                Tags = TagSerializer.Deserialize(entity.TagsJson),
            };
    }

    extension(UpdateGrantRequest request)
    {
        public Grant ApplyTo(GrantEntity entity) =>
            new()
            {
                GrantId = entity.GrantId,
                AccountId = entity.AccountId,
                Operation = entity.Operation,
                Unit = request.Unit,
                Quantity = request.Quantity,
                ValidFromUtc = request.ValidFromUtc,
                ValidToUtc = request.ValidToUtc,
                Tags = request.Tags,
            };
    }
}
