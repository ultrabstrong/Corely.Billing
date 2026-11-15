using Corely.Billing.Accounts.Models;
using Corely.Billing.Consumption.Entities;
using Corely.Billing.GrantRequests.Entities;
using Corely.Billing.Grants.Entities;
using Corely.Billing.Registrations.Entities;
using Corely.Common.Extensions;
using Corely.DataAccess.Interfaces.Repos;
using Microsoft.Extensions.Logging;

namespace Corely.Billing.Accounts.Processors;

internal class AccountDataProcessor(
    IRepo<ConsumptionEventEntity> consumptionRepo,
    IRepo<RegistrationEntity> registrationRepo,
    IRepo<GrantRequestEntity> grantRequestRepo,
    IRepo<GrantEntity> grantRepo,
    ILogger<AccountDataProcessor> logger
) : IAccountDataProcessor
{
    private readonly IRepo<ConsumptionEventEntity> _consumptionRepo = consumptionRepo.ThrowIfNull(
        nameof(consumptionRepo)
    );
    private readonly IRepo<RegistrationEntity> _registrationRepo = registrationRepo.ThrowIfNull(
        nameof(registrationRepo)
    );
    private readonly IRepo<GrantRequestEntity> _grantRequestRepo = grantRequestRepo.ThrowIfNull(
        nameof(grantRequestRepo)
    );
    private readonly IRepo<GrantEntity> _grantRepo = grantRepo.ThrowIfNull(nameof(grantRepo));
    private readonly ILogger<AccountDataProcessor> _logger = logger.ThrowIfNull(nameof(logger));

    public async Task<DeleteAccountDataResult> DeleteAccountDataAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var consumptionEvents = await _consumptionRepo.ExecuteDeleteAsync(
            e => e.AccountId == accountId,
            ct
        );
        var registrations = await _registrationRepo.ExecuteDeleteAsync(
            r => r.AccountId == accountId,
            ct
        );
        var grantRequests = await _grantRequestRepo.ExecuteDeleteAsync(
            r => r.AccountId == accountId,
            ct
        );
        var grants = await _grantRepo.ExecuteDeleteAsync(g => g.AccountId == accountId, ct);

        _logger.LogInformation(
            "Deleted the billing data of account {AccountId}: {ConsumptionEvents} consumption events, {Registrations} registrations, {GrantRequests} grant requests and {Grants} grants",
            accountId,
            consumptionEvents,
            registrations,
            grantRequests,
            grants
        );
        return new DeleteAccountDataResult(
            DeleteAccountDataResultCode.Success,
            string.Empty,
            consumptionEvents,
            registrations,
            grantRequests,
            grants
        );
    }
}
