using Corely.Billing.Quota.Models;
using Microsoft.AspNetCore.Http;

namespace Corely.Billing.Web.IAM.Extensions;

internal static class SettleQuotaResultCodeExtensions
{
    extension(SettleQuotaResultCode code)
    {
        public int StatusCode =>
            code switch
            {
                SettleQuotaResultCode.Success => StatusCodes.Status200OK,
                SettleQuotaResultCode.UnauthorizedError => StatusCodes.Status403Forbidden,
                SettleQuotaResultCode.NotRecordedError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
    }
}
