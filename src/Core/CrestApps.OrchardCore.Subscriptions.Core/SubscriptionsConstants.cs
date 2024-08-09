namespace CrestApps.OrchardCore.Subscriptions.Core;

public static class SubscriptionsConstants
{
    public const string Stereotype = "Subscription";

    public static class RouteName
    {
        public const string CreateSubscriptionEndpoint = "Subscriptions_StripeCreateSubscription";

        public const string CreatePaymentIntentEndpoint = "Subscriptions_StripeCreatePaymentIntent";

        public const string CreateSetupIntentEndpoint = "Subscriptions_StripeCreateSetupIntent";
    }

    public static class Features
    {
        public const string ModuleId = "CrestApps.OrchardCore.Subscriptions";

        public const string TenantOnboarding = "CrestApps.OrchardCore.Subscriptions.TenantOnboarding";

        public const string Stripe = "CrestApps.OrchardCore.Subscriptions.Stripe";
    }
}
