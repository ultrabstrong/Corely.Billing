using Corely.Billing.Quota.Models;
using Microsoft.AspNetCore.Http;

namespace Corely.Billing.Web.IAM.Extensions;

internal static class ReserveQuotaResultCodeExtensions
{
    extension(ReserveQuotaResultCode code)
    {
        public int StatusCode =>
            code switch
            {
                ReserveQuotaResultCode.Success => StatusCodes.Status200OK,
                ReserveQuotaResultCode.ValidationError => StatusCodes.Status400BadRequest,
                ReserveQuotaResultCode.UnauthorizedError => StatusCodes.Status403Forbidden,
                ReserveQuotaResultCode.NoGrantAvailableError => StatusCodes.Status409Conflict,
                ReserveQuotaResultCode.InsufficientQuotaError => StatusCodes.Status409Conflict,
                ReserveQuotaResultCode.NotRecordedError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
    }
}
