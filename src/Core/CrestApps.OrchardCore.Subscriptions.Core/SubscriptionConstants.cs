namespace CrestApps.OrchardCore.Subscriptions.Core;

public static class SubscriptionConstants
{
    public const string Stereotype = "Subscription";

    public const string SubscriptionSummaryWidgetType = "SubscriptionSummaryWidget";

    public const string PayLaterProcessorKey = "PayLater";

    public const string InitialFeeIdPrefix = "__InitialFee";

    public static class RouteName
    {
        public const string CreateSubscriptionEndpoint = "Subscriptions_StripeCreateSubscription";

        public const string CreateCheckoutSessionEndpoint = "Subscriptions_StripeCreateCheckoutSession";

        public const string CreatePaymentIntentEndpoint = "Subscriptions_StripeCreatePaymentIntent";

        public const string CreateSetupIntentEndpoint = "Subscriptions_StripeCreateSetupIntent";

        public const string CreatePayLaterEndpoint = "Subscriptions_CreatePayLater";
    }

    /// <summary>
    /// Rate-limit group names attached to the sensitive, anonymous-facing subscription routes. They are
    /// consumed by the optional Orchard Core <c>OrchardCore.RateLimits</c> module: when that feature is
    /// enabled and an administrator creates a policy targeting one of these groups, the matching routes
    /// are throttled automatically. Attaching the metadata is inert until such a policy exists, so it is
    /// always safe to declare regardless of whether the Rate Limits feature is enabled.
    /// </summary>
    public static class RateLimitGroups
    {
        /// <summary>
        /// The public subscription signup/checkout requests (the signup form and each flow step).
        /// </summary>
        public const string Checkout = "subscription-checkout";

        /// <summary>
        /// The anonymous payment endpoints that talk to the payment provider (intents, checkout
        /// sessions, and the pay-later confirmation).
        /// </summary>
        public const string Payment = "subscription-payment";
    }

    public static class Features
    {
        public const string Area = "CrestApps.OrchardCore.Subscriptions";

        public const string ReCaptcha = "CrestApps.OrchardCore.Subscriptions.ReCaptcha";

        public const string TenantOnboarding = "CrestApps.OrchardCore.Subscriptions.TenantOnboarding";
    }

    public static class StepKey
    {
        public const string UserRegistration = "UserRegistration";

        public const string TenantOnboarding = "TenantOnboarding";

        public const string Payment = "Payment";
    }
}
