using System.Globalization;

namespace Corely.Billing.Web.Extensions;

internal static class DateTimeExtensions
{
    extension(DateTime value)
    {
        public string IsoUtc() =>
            value.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }
}
