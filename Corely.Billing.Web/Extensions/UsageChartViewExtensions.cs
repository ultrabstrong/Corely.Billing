using Corely.Billing.Web.Components;

namespace Corely.Billing.Web.Extensions;

internal static class UsageChartViewExtensions
{
    extension(UsageChartView view)
    {
        public string Label() =>
            view switch
            {
                UsageChartView.Stacked => "Stacked",
                UsageChartView.SideBySide => "Side by side",
                UsageChartView.Remaining => "Remaining",
                UsageChartView.BurnUp => "Burn-up",
                UsageChartView.Share => "Share",
                _ => "Used",
            };

        public bool BreaksDown() =>
            view is UsageChartView.Stacked or UsageChartView.SideBySide or UsageChartView.Share;

        public string Caption(UsageBreakdown by) =>
            view switch
            {
                UsageChartView.Stacked => $"Used, by {by.Label()}",
                UsageChartView.SideBySide => $"Used, by {by.Label()}, side by side",
                UsageChartView.Remaining => "Remaining at the end of each period",
                UsageChartView.BurnUp => "Used so far, against the allowance",
                UsageChartView.Share => $"Share of use, by {by.Label()}",
                _ => "Used",
            };

        public string JsKind() =>
            view switch
            {
                UsageChartView.Stacked => "stacked",
                UsageChartView.SideBySide => "grouped",
                UsageChartView.Remaining => "remaining",
                UsageChartView.BurnUp => "burnup",
                UsageChartView.Share => "share",
                _ => "bar",
            };
    }
}
