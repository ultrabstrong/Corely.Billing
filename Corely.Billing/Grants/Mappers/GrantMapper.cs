using Corely.Billing.Grants.Entities;
using Corely.Billing.Grants.Models;
using Corely.Billing.Serialization;

namespace Corely.Billing.Grants.Mappers;

internal static class GrantMapper
{
    public static Grant ToGrant(this CreateGrantRequest request) =>
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

    public static GrantEntity ToEntity(this Grant grant) =>
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

    public static Grant ToModel(this GrantEntity entity) =>
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

    public static Grant ApplyTo(this UpdateGrantRequest request, GrantEntity entity) =>
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
