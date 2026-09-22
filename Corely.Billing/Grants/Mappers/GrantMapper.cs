using Corely.Billing;
using Corely.Billing.Grants.DataAccess;
using Corely.Billing.Grants.Models;

namespace Corely.Billing.Grants.Mappers;

internal static class GrantMapper
{
    public static GrantEntity ToEntity(this Grant model) =>
        new()
        {
            GrantId = model.GrantId,
            AccountId = model.AccountId,
            Quantity = model.Quantity,
            Unit = model.Unit,
            Operation = model.Operation,
            ValidFromUtc = model.ValidFromUtc,
            ValidToUtc = model.ValidToUtc,
            TagsJson = TagSerializer.Serialize(model.Tags),
        };
}
