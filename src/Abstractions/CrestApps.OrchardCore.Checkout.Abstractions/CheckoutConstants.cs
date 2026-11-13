namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Well-known identifiers used throughout the checkout framework.
/// </summary>
public static class CheckoutConstants
{
    /// <summary>
    /// The attempt policies Checkout registers. Each can be overridden by name under
    /// <c>CrestApps_Throttling:AttemptPolicies</c>.
    /// </summary>
    public static class AttemptPolicies
    {
        /// <summary>
        /// Begin-payment calls per network across every checkout: 30 per 10 minutes.
        /// </summary>
        public const string PaymentBeginNetwork = "payment-begin-network";

        /// <summary>
        /// Begin-payment calls in one checkout's lifetime: 8 per 24 hours.
        /// </summary>
        public const string PaymentBeginCheckout = "payment-begin-checkout";

        /// <summary>
        /// Declined or failed card payments per network: 5 per 30 minutes, then blocked 30 minutes.
        /// </summary>
        public const string PaymentDeclineNetwork = "payment-decline-network";

        /// <summary>
        /// Declined or failed card payments per signed-in customer: 5 per hour, then blocked an hour.
        /// </summary>
        public const string PaymentDeclineCustomer = "payment-decline-customer";

        /// <summary>
        /// Declined or failed card payments store-wide: 30 per 10 minutes, then the store's decline-spike action applies
        /// for 30 minutes.
        /// </summary>
        public const string PaymentDeclineStore = "payment-decline-store";

        /// <summary>
        /// Failed confirmations of one payment attempt, such as cards tried one after another against one Stripe client
        /// secret: 3, then the attempt is canceled at the provider and the buyer must begin a new payment, which the gate can
        /// refuse.
        /// </summary>
        public const string PaymentDeclineAttempt = "payment-decline-attempt";

        /// <summary>
        /// Provider failure events counted by the card-testing protection: each once within 3 days, so a redelivered
        /// event counts no decline again.
        /// </summary>
        public const string PaymentDeclineEvent = "payment-decline-event";

        /// <summary>
        /// Status polls per network and checkout: 120 per 5 minutes.
        /// </summary>
        public const string PaymentStatus = "payment-status";

        /// <summary>
        /// Checkouts started per network: 60 per hour.
        /// </summary>
        public const string CheckoutStartNetwork = "checkout-start-network";

        /// <summary>
        /// Wrong discount codes per network: 10 per 10 minutes, then blocked 10 minutes.
        /// </summary>
        public const string CouponCode = "coupon-code";
    }

    /// <summary>
    /// The YesSql collection the coupon catalog is stored in.
    /// </summary>
    /// <summary>
    /// The cookie a discount link keeps its code in until the buyer checks out.
    /// </summary>
    public const string DiscountLinkCookieName = "orchard-discount-code";

    /// <summary>
    /// The most unique codes generated for a coupon at once.
    /// </summary>
    public const int MaxGeneratedCodes = 5000;

    public const string CouponCollectionName = "Coupon";

    /// <summary>
    /// The name of the recipe and deployment step that sets coupons.
    /// </summary>
    public const string CouponsRecipeStep = "Coupons";

    /// <summary>
    /// The name of the cookie that carries a guest's checkout ownership tokens.
    /// </summary>
    public const string GuestTokenCookieName = "checkout_owner";

    /// <summary>
    /// The data-protection purpose for the guest ownership cookie. It is unique to checkout so the cookie
    /// cannot be replayed against another feature.
    /// </summary>
    public const string GuestTokenProtectorPurpose = "CrestApps.OrchardCore.Checkout.GuestOwnership.v1";

    /// <summary>
    /// The reserved step key for the payment step that every paid checkout flow contains.
    /// </summary>
    public const string PaymentStepKey = "Payment";

    /// <summary>
    /// The site settings group of the checkout settings, under Settings › Commerce › Checkout.
    /// </summary>
    public const string SettingsGroupId = "checkout";

    /// <summary>
    /// The feature identifiers exposed by the checkout module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The core checkout feature that provides the provider-agnostic checkout and payment framework.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Checkout";

        /// <summary>
        /// Payment Rate Limits: the request limits of the payment, status, start and code routes, as an extension of
        /// Orchard Core's Rate Limits feature (which it enables). The card-testing gate and the attempt policies work
        /// without it.
        /// </summary>
        public const string RateLimits = "CrestApps.OrchardCore.Checkout.RateLimits";

        /// <summary>
        /// The development test payment provider. It confirms every payment without moving money, so it is only
        /// active in the Development environment unless configuration explicitly allows it elsewhere.
        /// </summary>
        public const string TestProvider = "CrestApps.OrchardCore.Checkout.TestProvider";

        /// <summary>
        /// The checkout API under <c>api/commerce/v1</c>: reading a checkout, its codes, paying it and completing it.
        /// </summary>
        public const string Api = "CrestApps.OrchardCore.Checkout.Api";
    }

    /// <summary>
    /// The named routes of the public checkout experience.
    /// </summary>
    /// <summary>
    /// The named actions a checkout form can post, other than paying.
    /// </summary>
    /// <remarks>
    /// The payment script takes over the form's submit event, so anything else the page needs to submit has
    /// to identify itself. Without a name the server cannot tell "apply this code" from "take my money".
    /// </remarks>
    public static class FormActions
    {
        /// <summary>
        /// The name of the submit button that carries the action.
        /// </summary>
        public const string FieldName = "checkout-action";

        /// <summary>
        /// Saves the entered promotion code and re-renders the step with the recalculated invoice.
        /// </summary>
        public const string ApplyCoupon = "apply-coupon";

        /// <summary>
        /// Takes one promotion code off, the one after the prefix, and re-renders the step.
        /// </summary>
        public const string RemoveCouponPrefix = "remove-coupon:";

        /// <summary>
        /// Applies a balance, such as a gift card, to the checkout and shows the step again.
        /// </summary>
        public const string ApplyTender = "apply-tender";

        /// <summary>
        /// Takes a balance off the checkout: the provider key and the reference follow, separated by a colon.
        /// </summary>
        public const string RemoveTenderPrefix = "remove-tender:";

        /// <summary>
        /// Saves what was changed on a one-page checkout and shows it again with the new total, instead of paying.
        /// </summary>
        public const string Update = "update";
    }

    public static class RouteNames
    {
        /// <summary>
        /// The route that displays a step of a pending checkout.
        /// </summary>
        public const string Step = "CheckoutStep";

        /// <summary>
        /// The route that displays the confirmation of a completed checkout.
        /// </summary>
        public const string Confirmation = "CheckoutConfirmation";

        /// <summary>
        /// The discount link, <c>discount/{code}</c>, which keeps a code for the buyer's next checkout.
        /// </summary>
        public const string DiscountLink = "CheckoutDiscountLink";

        /// <summary>
        /// The endpoint that begins payment with the selected provider.
        /// </summary>
        public const string BeginPayment = "CheckoutBeginPayment";

        /// <summary>
        /// The endpoint the client polls while a provider settles a payment.
        /// </summary>
        public const string PaymentStatus = "CheckoutPaymentStatus";
    }

    /// <summary>
    /// Rate-limit group names attached to the sensitive, anonymous-facing checkout routes. They are
    /// consumed by the optional Orchard Core <c>OrchardCore.RateLimits</c> module.
    /// </summary>
    public static class RateLimitGroups
    {
        /// <summary>
        /// The public checkout requests (the checkout form and each flow step).
        /// </summary>
        public const string Checkout = "checkout";

        /// <summary>
        /// Beginning a payment, in the browser and over the API: 30 per 5 minutes per network with the Payment Rate Limits
        /// feature.
        /// </summary>
        public const string Payment = "checkout-payment";

        /// <summary>
        /// Polling a payment's status and completing a checkout over the API: 60 a minute per client. Kept apart from
        /// <see cref="Payment"/>, so a limit tight enough to matter for payments doesn't break status polling.
        /// </summary>
        public const string PaymentStatus = "checkout-payment-status";

        /// <summary>
        /// Starting a checkout: 30 per 10 minutes per network.
        /// </summary>
        public const string Start = "checkout-start";

        /// <summary>
        /// Applying a discount code or a gift card, and the discount link: 20 per 5 minutes per client.
        /// </summary>
        public const string Code = "checkout-code";
    }
}
