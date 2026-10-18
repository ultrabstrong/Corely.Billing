using Corely.Billing.GrantRequests.Entities;
using Corely.Billing.GrantRequests.Models;
using Corely.Billing.Grants.Models;
using Corely.Billing.Serialization;

namespace Corely.Billing.GrantRequests.Mappers;

internal static class GrantRequestMapper
{
    extension(RequestGrantRequest request)
    {
        public GrantRequest ToGrantRequest(DateTime requestedUtc) =>
            new()
            {
                GrantRequestId = Guid.CreateVersion7(),
                AccountId = request.AccountId,
                Operation = request.Operation,
                Unit = request.Unit,
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
                entity.Unit,
                entity.Quantity,
                approval.ValidFromUtc,
                approval.ValidToUtc,
                TagSerializer.Deserialize(entity.TagsJson)
            );
    }
}
