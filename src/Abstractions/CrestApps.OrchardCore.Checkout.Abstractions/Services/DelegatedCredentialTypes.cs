namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The kinds of payment credential an AI agent brings (<see cref="CheckoutPaymentDataKeys.DelegatedCredentialType"/>).
/// </summary>
public static class DelegatedCredentialTypes
{
    /// <summary>
    /// A Stripe shared payment token (<c>spt_…</c>), which the agent's platform issued to the store's Stripe account.
    /// </summary>
    public const string StripeSharedPaymentToken = "stripe_shared_payment_token";

    /// <summary>
    /// A Google Pay payment token whose gateway is the store's payment provider.
    /// </summary>
    public const string GooglePayToken = "google_pay_token";

    /// <summary>
    /// A test credential, which only a test payment provider accepts.
    /// </summary>
    public const string Test = "test";
}
