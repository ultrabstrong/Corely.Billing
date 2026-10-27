using Corely.Billing.Models;
using Microsoft.AspNetCore.Http;

namespace Corely.Billing.Web.IAM.Extensions;

internal static class ModifyResultCodeExtensions
{
    extension(ModifyResultCode code)
    {
        public int StatusCode =>
            code switch
            {
                ModifyResultCode.Success => StatusCodes.Status200OK,
                ModifyResultCode.ValidationError => StatusCodes.Status400BadRequest,
                ModifyResultCode.UnauthorizedError => StatusCodes.Status403Forbidden,
                ModifyResultCode.NotFoundError => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError,
            };
    }
}
