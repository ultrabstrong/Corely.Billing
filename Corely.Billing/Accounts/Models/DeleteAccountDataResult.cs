namespace Corely.Billing.Accounts.Models;

public record DeleteAccountDataResult(
    DeleteAccountDataResultCode ResultCode,
    string Message,
    int ConsumptionEvents = 0,
    int Registrations = 0,
    int GrantRequests = 0,
    int Grants = 0
);
