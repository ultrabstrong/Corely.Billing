using Corely.Billing.Grants.Models;
using Corely.Billing.Registrations.Entities;
using Corely.Billing.Registrations.Enums;
using Corely.Billing.Registrations.Models;

namespace Corely.Billing.Registrations.Mappers;

internal static class RegistrationMapper
{
    extension(RegistrationEntity entity)
    {
        public RegistrationStatus StatusOn(Grant? grant, DateTime utcNow) =>
            entity.ReleasedUtc is not null ? RegistrationStatus.Released
            : grant is null || grant.ValidToUtc < utcNow ? RegistrationStatus.GrantEnded
            : grant.Disabled ? RegistrationStatus.GrantDisabled
            : RegistrationStatus.Active;

        public Registration ToModel(Grant? grant, DateTime utcNow) =>
            new(
                entity.RegistrationId,
                entity.AccountId,
                entity.GrantId,
                entity.Operation,
                entity.Reference,
                entity.Description,
                entity.RegisteredUtc,
                entity.ReleasedUtc,
                entity.StatusOn(grant, utcNow)
            );

        public RegistrationEntity MovedTo(Guid grantId, DateTime registeredUtc) =>
            new()
            {
                RegistrationId = Guid.CreateVersion7(),
                AccountId = entity.AccountId,
                GrantId = grantId,
                Operation = entity.Operation,
                Reference = entity.Reference,
                Description = entity.Description,
                RegisteredUtc = registeredUtc,
            };
    }

    extension(RegisterRequest request)
    {
        public RegistrationEntity ToEntity(Guid grantId, DateTime registeredUtc) =>
            new()
            {
                RegistrationId = Guid.CreateVersion7(),
                AccountId = request.AccountId,
                GrantId = grantId,
                Operation = request.Operation,
                Reference = string.IsNullOrWhiteSpace(request.Reference)
                    ? null
                    : request.Reference.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim(),
                RegisteredUtc = registeredUtc,
            };
    }
}
