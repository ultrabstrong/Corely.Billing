using Corely.Billing.Usage;

namespace Corely.Billing.Prices.Models;

public record UsagePrice(UsageOperation Operation, Money UnitPrice);
