namespace Corely.Billing.Telemetry;

public interface IBillingTelemetry
{
    void Increment(string metric);
    void Record(string metric, double value);
}
