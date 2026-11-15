using Corely.Billing.Accounts.Models;
using Corely.Billing.Services;
using Corely.IAM.Accounts.Models;
using Corely.IAM.Audits.Models;
using Corely.IAM.Audits.Providers;
using Corely.IAM.Security.Enums;

namespace Corely.Billing.IAM.Auditing;

internal sealed class AccountDataAuditDecorator(
    IAccountDataService inner,
    IAuditProvider auditProvider
) : IAccountDataService
{
    private const string SERVICE = nameof(IAccountDataService);

    public Task<DeleteAccountDataResult> DeleteAccountDataAsync(
        Guid accountId,
        CancellationToken ct = default
    ) =>
        auditProvider.RecordAsync(
            new AuditCall(SERVICE, AuthAction.Delete, Account.RESOURCE_TYPE)
            {
                AccountId = accountId,
                ResourceIds = [accountId],
            },
            () => inner.DeleteAccountDataAsync(accountId, ct),
            r => AuditOutcome.Of(r.ResultCode)
        );
}
