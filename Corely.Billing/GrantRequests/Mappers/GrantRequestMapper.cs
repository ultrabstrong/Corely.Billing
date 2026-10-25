using Corely.Billing.GrantRequests.Entities;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Serialization;
using Corely.Billing.Usage;

namespace Corely.Billing.GrantRequests.Mappers;

internal static class GrantRequestMapper
{
    extension(RequestGrantRequest request)
    {
        public GrantRequest ToGrantRequest(
            UsageOperationDefinition definition,
            DateTime requestedUtc
        ) =>
            new()
            {
                GrantRequestId = Guid.CreateVersion7(),
                AccountId = request.AccountId,
                Operation = request.Operation,
                Kind = definition.Kind,
                Unit = definition.Unit,
                Quantity = request.Quantity,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                RequestedByUserId = request.RequestedByUserId,
                RequestedUtc = requestedUtc,
                Status = GrantRequestStatus.Pending,
                Tags = request.Tags,
            };
    }

    extension(GrantRequest grantRequest)
    {
        public GrantRequestEntity ToEntity() =>
            new()
            {
                GrantRequestId = grantRequest.GrantRequestId,
                AccountId = grantRequest.AccountId,
                Operation = grantRequest.Operation,
                Kind = grantRequest.Kind,
                Unit = grantRequest.Unit,
                Quantity = grantRequest.Quantity,
                Note = grantRequest.Note,
                RequestedByUserId = grantRequest.RequestedByUserId,
                RequestedUtc = grantRequest.RequestedUtc,
                Status = grantRequest.Status,
                DecidedUtc = grantRequest.DecidedUtc,
                DecidedByUserId = grantRequest.DecidedByUserId,
                DecisionReason = grantRequest.DecisionReason,
                GrantId = grantRequest.GrantId,
                TagsJson = TagSerializer.Serialize(grantRequest.Tags),
            };
    }

    extension(GrantRequestEntity entity)
    {
        public GrantRequest ToModel() =>
            new()
            {
                GrantRequestId = entity.GrantRequestId,
                AccountId = entity.AccountId,
                Operation = entity.Operation,
                Kind = entity.Kind,
                Unit = entity.Unit,
                Quantity = entity.Quantity,
                Note = entity.Note,
                RequestedByUserId = entity.RequestedByUserId,
                RequestedUtc = entity.RequestedUtc,
                Status = entity.Status,
                DecidedUtc = entity.DecidedUtc,
                DecidedByUserId = entity.DecidedByUserId,
                DecisionReason = entity.DecisionReason,
                GrantId = entity.GrantId,
                Tags = TagSerializer.Deserialize(entity.TagsJson),
            };

        public CreateGrantRequest ToCreateGrantRequest(ApproveGrantRequestRequest approval) =>
            new(
                entity.AccountId,
                entity.Operation,
                entity.Kind,
                entity.Quantity,
                approval.ValidFromUtc,
                approval.ValidToUtc,
                entity.Unit,
                TagSerializer.Deserialize(entity.TagsJson)
            );
    }
}
