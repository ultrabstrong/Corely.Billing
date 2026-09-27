namespace Corely.Billing.Web.Extensions;

internal static class EnumerableExtensions
{
    extension(IEnumerable<string?> cells)
    {
        public string ToCsvRow() => string.Join(',', cells.Select(c => c.CsvCell()));
    }
}
