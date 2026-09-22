namespace Corely.Billing.Quota.Models;

/// <summary>
/// What a quantity of work would draw from the grants available to it.
/// </summary>
/// <param name="Shares">Per grant, in the order the policy spends them.</param>
/// <param name="Shortfall">
/// What no grant had room for. The caller decides what that means: before the work, it is
/// insufficient quota and the work is refused; afterwards, the provider has already been paid and
/// the overdraft lands on the last grant instead.
/// </param>
public sealed record GrantSplit(IReadOnlyList<GrantShare> Shares, long Shortfall);
