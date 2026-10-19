namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Well-known identifiers used throughout the checkout framework.
/// </summary>
public static class CheckoutConstants
{
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
        /// The development test payment provider. It confirms every payment without moving money, so it is only
        /// active in the Development environment unless configuration explicitly allows it elsewhere.
        /// </summary>
        public const string TestProvider = "CrestApps.OrchardCore.Checkout.TestProvider";
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
        /// The anonymous payment endpoints that talk to a payment provider.
        /// </summary>
        public const string Payment = "checkout-payment";
    }
}
