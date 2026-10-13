namespace Corely.Billing.Web.Extensions;

internal static class GuidExtensions
{
    private const int SHORT_LENGTH = 8;

    extension(Guid id)
    {
        public string ShortText() => id.ToString()[^SHORT_LENGTH..];
    }
}
