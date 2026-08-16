namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Defines constants used by the Stripe integration.
/// </summary>
public static class StripeConstants
{
    /// <summary>
    /// The payment processor key used to identify Stripe within payment records.
    /// </summary>
    public const string ProcessorKey = "Stripe";

    /// <summary>
    /// Defines endpoint route names used by the Stripe module.
    /// </summary>
    public static class RouteName
    {
        /// <summary>
        /// The route name for the Stripe webhook endpoint.
        /// </summary>
        public const string CreateWebhookEndpoint = "StripeWebhook";

        /// <summary>
        /// The route name for the endpoint that creates Stripe PaymentIntents.
        /// </summary>
        public const string CreatePaymentIntentEndpoint = "StripeCreatePaymentIntent";

        /// <summary>
        /// The route name for the endpoint that creates Stripe SetupIntents.
        /// </summary>
        public const string CreateSetupIntentEndpoint = "StripeCreateSetupIntent";

        /// <summary>
        /// The route name for the endpoint that creates Stripe subscriptions.
        /// </summary>
        public const string CreateSubscriptionEndpoint = "StripeCreateSubscription";
    }

    /// <summary>
    /// Defines feature identifiers for the Stripe module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The Orchard Core feature identifier for the Stripe module.
        /// </summary>
        public const string ModuleId = "CrestApps.OrchardCore.Stripe";
    }
}
