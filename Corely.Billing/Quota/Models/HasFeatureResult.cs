using Corely.Billing.Grants.Models;

namespace Corely.Billing.Quota.Models;

public record HasFeatureResult(
    QuotaReadResultCode ResultCode,
    string Message,
    bool HasFeature,
    IReadOnlyList<Grant> Grants
);
