namespace CrestApps.OrchardCore.Stripe.Core;

public static class StripeConstants
{
    public const string ProcessorKey = "Stripe";

    public static class RouteName
    {
        public const string CreateWebhookEndpoint = "StripeWebhook";

        public const string CreatePaymentIntentEndpoint = "StripeCreatePaymentIntent";

        public const string CreateSetupIntentEndpoint = "StripeCreateSetupIntent";

        public const string CreateSubscriptionEndpoint = "StripeCreateSubscription";
    }

    public static class Feature
    {
        public const string ModuleId = "CrestApps.OrchardCore.Stripe";
    }
}
