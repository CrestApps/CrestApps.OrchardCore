namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Defines shared constants used by the subscriptions module.
/// </summary>
public static class SubscriptionConstants
{
    /// <summary>
    /// The content type stereotype used to identify subscription content types.
    /// </summary>
    public const string Stereotype = "Subscription";

    /// <summary>
    /// The content type name for the subscription summary dashboard widget.
    /// </summary>
    public const string SubscriptionSummaryWidgetType = "SubscriptionSummaryWidget";

    /// <summary>
    /// The payment processor key used for pay-later subscription flows.
    /// </summary>
    public const string PayLaterProcessorKey = "PayLater";

    /// <summary>
    /// The prefix used to identify initial-fee payment entries.
    /// </summary>
    public const string InitialFeeIdPrefix = "__InitialFee";

    /// <summary>
    /// Defines named routes used by subscription payment and checkout endpoints.
    /// </summary>
    public static class RouteName
    {
        /// <summary>
        /// The route name for creating a Stripe subscription.
        /// </summary>
        public const string CreateSubscriptionEndpoint = "Subscriptions_StripeCreateSubscription";

        /// <summary>
        /// The route name for creating a Stripe checkout session.
        /// </summary>
        public const string CreateCheckoutSessionEndpoint = "Subscriptions_StripeCreateCheckoutSession";

        /// <summary>
        /// The route name for creating a Stripe payment intent.
        /// </summary>
        public const string CreatePaymentIntentEndpoint = "Subscriptions_StripeCreatePaymentIntent";

        /// <summary>
        /// The route name for creating a Stripe setup intent.
        /// </summary>
        public const string CreateSetupIntentEndpoint = "Subscriptions_StripeCreateSetupIntent";

        /// <summary>
        /// The route name for confirming a pay-later subscription.
        /// </summary>
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

    /// <summary>
    /// Defines feature identifiers for subscription module features.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Orchard Core area name for the subscriptions module.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Subscriptions";

        /// <summary>
        /// The feature identifier for subscription reCAPTCHA integration.
        /// </summary>
        public const string ReCaptcha = "CrestApps.OrchardCore.Subscriptions.ReCaptcha";

        /// <summary>
        /// The feature identifier for subscription tenant onboarding.
        /// </summary>
        public const string TenantOnboarding = "CrestApps.OrchardCore.Subscriptions.TenantOnboarding";
    }

    /// <summary>
    /// Defines keys for built-in subscription checkout flow steps.
    /// </summary>
    public static class StepKey
    {
        /// <summary>
        /// The step key for user registration.
        /// </summary>
        public const string UserRegistration = "UserRegistration";

        /// <summary>
        /// The step key for tenant onboarding.
        /// </summary>
        public const string TenantOnboarding = "TenantOnboarding";

        /// <summary>
        /// The step key for payment.
        /// </summary>
        public const string Payment = "Payment";
    }
}
