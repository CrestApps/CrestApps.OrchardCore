namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The provider-data keys a caller uses to ask any provider that supports saved payment methods
/// (<see cref="PaymentProviderCapabilities.SupportsSavedPaymentMethods"/>) to keep a payment method or to charge
/// one it kept earlier. They are passed in <see cref="BeginPaymentOptions.ProviderData"/>.
/// </summary>
/// <remarks>
/// Only server code sets the off-session keys. A charge without the payer present spends a payment method the
/// payer saved earlier, so it must never be reachable from a request the payer's browser can shape.
/// </remarks>
public static class CheckoutPaymentDataKeys
{
    /// <summary>
    /// Set to <c>"true"</c> to keep the payment method used for this payment so it can be charged again later
    /// without the payer present. The provider tells the gateway the payment method is for future use, which is
    /// what lets a later charge skip the payer's authentication.
    /// </summary>
    public const string SavePaymentMethod = "savePaymentMethod";

    /// <summary>
    /// Set to <c>"true"</c> to charge a saved payment method now, server-side, without the payer present. The
    /// saved method is named by <see cref="SavedCustomerReference"/> and <see cref="SavedPaymentMethodReference"/>.
    /// </summary>
    public const string OffSession = "offSession";

    /// <summary>
    /// The provider's reference for the customer a saved payment method belongs to
    /// (<see cref="SavedPaymentMethod.CustomerReference"/>).
    /// </summary>
    public const string SavedCustomerReference = "savedCustomerReference";

    /// <summary>
    /// The provider's reference for the saved payment method (<see cref="SavedPaymentMethod.PaymentMethodReference"/>).
    /// </summary>
    public const string SavedPaymentMethodReference = "savedPaymentMethodReference";

    /// <summary>
    /// Builds the provider data that charges a saved payment method off-session.
    /// </summary>
    /// <param name="paymentMethod">The saved payment method to charge.</param>
    public static Dictionary<string, string> ForOffSessionCharge(SavedPaymentMethod paymentMethod)
    {
        ArgumentNullException.ThrowIfNull(paymentMethod);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OffSession] = "true",
            [SavedCustomerReference] = paymentMethod.CustomerReference,
            [SavedPaymentMethodReference] = paymentMethod.PaymentMethodReference,
        };
    }

    /// <summary>
    /// Gets whether the provider data turns on the given flag.
    /// </summary>
    /// <param name="providerData">The provider data, which may be <see langword="null"/>.</param>
    /// <param name="key">The flag key, for example <see cref="SavePaymentMethod"/>.</param>
    public static bool IsSet(IReadOnlyDictionary<string, string> providerData, string key)
        => providerData is not null &&
            providerData.TryGetValue(key, out var value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
