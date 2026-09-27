namespace Corely.Billing.Web.Extensions;

internal static class StringExtensions
{
    private static readonly char[] QuoteWhen = [',', '"', '\r', '\n'];
    private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];

    extension(string? value)
    {
        // RFC 4180, and a leading apostrophe on anything a spreadsheet would run as a formula:
        // providers and tags are caller-supplied text.
        public string CsvCell()
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var cell = FormulaStarts.Contains(value[0]) ? "'" + value : value;
            return cell.IndexOfAny(QuoteWhen) >= 0 || cell != cell.Trim()
                ? $"\"{cell.Replace("\"", "\"\"")}\""
                : cell;
        }
    }
}
