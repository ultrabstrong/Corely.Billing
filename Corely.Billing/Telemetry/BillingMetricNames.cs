namespace Corely.Billing.Telemetry;

public static class BillingMetricNames
{
    public static class Quota
    {
        public const string GRANT_FOUND = "quota.lookup.grant_found";
        public const string GRANT_NOT_FOUND = "quota.lookup.grant_not_found";
        public const string RESERVATION_SPANNED_GRANTS = "quota.reservation.spanned_grants";
        public const string SETTLED_QUANTITY = "quota.settlement.quantity";
        public const string GRANT_OVERDRAWN = "quota.settlement.grant_overdrawn";

        public const string GRANTS_RUNNING_LOW = "quota.settlement.grants_running_low";
    }

    public static class Grants
    {
        public const string GRANT_SAVED = "entitlements.grant.saved";
        public const string GRANT_UPDATED = "entitlements.grant.updated";
        public const string GRANT_DELETED = "entitlements.grant.deleted";
        public const string GRANT_DISABLED = "entitlements.grant.disabled";
        public const string GRANT_ENABLED = "entitlements.grant.enabled";
        public const string GRANT_QUANTITY = "entitlements.grant.quantity";
        public const string GRANT_VALIDITY_DAYS = "entitlements.grant.validity_days";
        public const string GRANT_ACTIVE_COUNT = "entitlements.grant.active_count";
        public const string GRANT_TOTAL_COUNT = "entitlements.grant.total_count";
    }

    public static class Registrations
    {
        public const string REGISTERED = "entitlements.registration.registered";
        public const string REFUSED = "entitlements.registration.refused";
        public const string RELEASED = "entitlements.registration.released";
        public const string MOVED = "entitlements.registration.moved";
    }

    public static class GrantRequests
    {
        public const string GRANT_REQUEST_CREATED = "entitlements.grant_request.created";
        public const string GRANT_REQUEST_APPROVED = "entitlements.grant_request.approved";
        public const string GRANT_REQUEST_DECLINED = "entitlements.grant_request.declined";
        public const string GRANT_REQUEST_QUANTITY = "entitlements.grant_request.quantity";
    }

    public static class Consumption
    {
        public const string CONSUMPTION_QUANTITY = "metering.consumption.quantity";
        public const string CONSUMPTION_TOTAL_QUERIED = "metering.consumption.total_queried";
        public const string CONSUMPTION_GRANTS_QUERIED = "metering.consumption.grants_queried";
        public const string CONSUMPTION_TIMESERIES_QUERIED =
            "metering.consumption.timeseries_queried";
        public const string CONSUMPTION_EVENTS_LISTED = "metering.consumption.events_listed";

        public const string RESERVATION_TAKEN = "metering.reservation.taken";
        public const string RESERVATION_QUANTITY = "metering.reservation.quantity";
        public const string RESERVATION_SETTLED = "metering.reservation.settled";
        public const string RESERVATION_RELEASED = "metering.reservation.released";
        public const string RESERVATION_ABANDONED = "metering.reservation.abandoned";
    }
}
