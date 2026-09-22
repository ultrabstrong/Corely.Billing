namespace Corely.Billing.Models;

public record RetrieveSingleResult<T>(RetrieveResultCode ResultCode, string Message, T? Item);
