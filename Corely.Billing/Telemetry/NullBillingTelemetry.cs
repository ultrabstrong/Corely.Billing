namespace Corely.Billing.Telemetry;

internal sealed class NullBillingTelemetry : IBillingTelemetry
{
    public void Increment(string metric) { }

    public void Record(string metric, double value) { }
}
