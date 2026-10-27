using Corely.Billing.Models;
using Microsoft.AspNetCore.Http;

namespace Corely.Billing.Web.IAM.Extensions;

internal static class RetrieveResultCodeExtensions
{
    extension(RetrieveResultCode code)
    {
        public int StatusCode =>
            code switch
            {
                RetrieveResultCode.Success => StatusCodes.Status200OK,
                RetrieveResultCode.UnauthorizedError => StatusCodes.Status403Forbidden,
                RetrieveResultCode.NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError,
            };
    }
}
