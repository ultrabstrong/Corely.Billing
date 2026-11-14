namespace Corely.Billing.Quota.Models;

internal record GrantSplit(IReadOnlyList<GrantShare> Shares, decimal Shortfall);
