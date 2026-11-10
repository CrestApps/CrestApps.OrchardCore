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
    /// A payment credential an AI agent brought for this one payment, such as a Stripe shared payment token or a
    /// Google Pay token. A provider that accepts it
    /// (<see cref="PaymentProviderCapabilities.AcceptsDelegatedCredentials"/>) charges it on the server, with no
    /// browser. Only server code sets it, never a request the payer's browser can shape.
    /// </summary>
    public const string DelegatedCredential = "delegatedCredential";

    /// <summary>
    /// The kind of <see cref="DelegatedCredential"/>, one of <see cref="DelegatedCredentialTypes"/>.
    /// </summary>
    public const string DelegatedCredentialType = "delegatedCredentialType";

    /// <summary>
    /// The outcome of a 3-D Secure authentication run outside the provider (<see cref="ThreeDSecureResult.Outcome"/>),
    /// one of <see cref="ThreeDSecureOutcomes"/>. A provider that accepts it
    /// (<see cref="PaymentProviderCapabilities.AcceptsExternalAuthentication"/>) charges with the result instead of
    /// running its own authentication. Only server code sets the <c>ThreeDSecure…</c> keys; write them with
    /// <see cref="ThreeDSecureResult.CopyTo"/> and read them with <see cref="ThreeDSecureResult.From"/>.
    /// </summary>
    public const string ThreeDSecureOutcome = "threeDSecureOutcome";

    /// <summary>
    /// The 3-D Secure authentication value (<see cref="ThreeDSecureResult.Cryptogram"/>).
    /// </summary>
    public const string ThreeDSecureCryptogram = "threeDSecureCryptogram";

    /// <summary>
    /// The 3-D Secure electronic commerce indicator (<see cref="ThreeDSecureResult.ElectronicCommerceIndicator"/>).
    /// </summary>
    public const string ThreeDSecureElectronicCommerceIndicator = "threeDSecureEci";

    /// <summary>
    /// The 3-D Secure directory server transaction id (<see cref="ThreeDSecureResult.TransactionId"/>).
    /// </summary>
    public const string ThreeDSecureTransactionId = "threeDSecureTransactionId";

    /// <summary>
    /// The 3-D Secure version that ran (<see cref="ThreeDSecureResult.Version"/>).
    /// </summary>
    public const string ThreeDSecureVersion = "threeDSecureVersion";

    /// <summary>
    /// The ARes <c>transStatus</c> of the 3-D Secure authentication, when known
    /// (<see cref="ThreeDSecureResult.TransactionStatus"/>).
    /// </summary>
    public const string ThreeDSecureTransactionStatus = "threeDSecureTransStatus";

    /// <summary>
    /// Gets whether a key is one only server code may set: the off-session, saved payment method, delegated credential
    /// and external 3-D Secure keys. A request a browser can shape must never carry them.
    /// </summary>
    /// <param name="key">The key.</param>
    public static bool IsServerOnly(string key)
        => key is OffSession
            or SavedCustomerReference
            or SavedPaymentMethodReference
            or SavePaymentMethod
            or DelegatedCredential
            or DelegatedCredentialType
            or ThreeDSecureOutcome
            or ThreeDSecureCryptogram
            or ThreeDSecureElectronicCommerceIndicator
            or ThreeDSecureTransactionId
            or ThreeDSecureVersion
            or ThreeDSecureTransactionStatus;

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
    /// Gets the value the provider data holds for a key, or <see langword="null"/> when it holds none or an empty one.
    /// </summary>
    /// <param name="providerData">The provider data, which may be <see langword="null"/>.</param>
    /// <param name="key">The key, for example <see cref="SavedPaymentMethodReference"/>.</param>
    public static string GetValue(IReadOnlyDictionary<string, string> providerData, string key)
        => providerData is not null && providerData.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : null;

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
