using Corely.Billing.Registrations.Models;
using Microsoft.AspNetCore.Http;

namespace Corely.Billing.Web.IAM.Extensions;

internal static class RegisterResultCodeExtensions
{
    extension(RegisterResultCode code)
    {
        public int StatusCode =>
            code switch
            {
                RegisterResultCode.Registered => StatusCodes.Status200OK,
                RegisterResultCode.ValidationError => StatusCodes.Status400BadRequest,
                RegisterResultCode.WrongKindError => StatusCodes.Status400BadRequest,
                RegisterResultCode.UnauthorizedError => StatusCodes.Status403Forbidden,
                RegisterResultCode.AlreadyRegisteredError => StatusCodes.Status409Conflict,
                RegisterResultCode.NoCapacityError => StatusCodes.Status409Conflict,
                RegisterResultCode.NoActiveGrantError => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
    }
}
