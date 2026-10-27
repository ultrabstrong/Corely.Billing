using Corely.Billing.Quota.Models;
using Microsoft.AspNetCore.Http;

namespace Corely.Billing.Web.IAM.Extensions;

internal static class QuotaReadResultCodeExtensions
{
    extension(QuotaReadResultCode code)
    {
        public int StatusCode =>
            code switch
            {
                QuotaReadResultCode.Success => StatusCodes.Status200OK,
                QuotaReadResultCode.WrongKindError => StatusCodes.Status400BadRequest,
                QuotaReadResultCode.UnauthorizedError => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError,
            };
    }
}
