using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The result of a 3-D Secure authentication run outside the payment provider, such as by an AI agent or an
/// authentication service, which a provider that accepts it
/// (<see cref="PaymentProviderCapabilities.AcceptsExternalAuthentication"/>) charges a card with instead of running its
/// own. It travels in <see cref="BeginPaymentOptions.ProviderData"/> under the <c>ThreeDSecure…</c> keys of
/// <see cref="CheckoutPaymentDataKeys"/>, which only server code sets.
/// </summary>
public sealed partial class ThreeDSecureResult
{
    private static readonly HashSet<string> _electronicCommerceIndicators = new(StringComparer.Ordinal) { "01", "02", "05", "06", "07" };

    private static readonly HashSet<string> _transactionStatuses = new(StringComparer.Ordinal) { "A", "C", "I", "N", "R", "U", "Y" };

    /// <summary>
    /// The length of a cryptogram: 20 bytes, base64-encoded.
    /// </summary>
    public const int CryptogramLength = 28;

    /// <summary>
    /// The longest transaction id taken: a directory server transaction id is a 36-character UUID.
    /// </summary>
    public const int MaxTransactionIdLength = 64;

    /// <summary>
    /// Gets or sets the outcome, one of <see cref="ThreeDSecureOutcomes"/>.
    /// </summary>
    public string Outcome { get; set; }

    /// <summary>
    /// Gets or sets the authentication value (CAVV, AAV or AEVV): 20 bytes, base64-encoded into 28 characters.
    /// </summary>
    public string Cryptogram { get; set; }

    /// <summary>
    /// Gets or sets the electronic commerce indicator (ECI): <c>01</c>, <c>02</c>, <c>05</c>, <c>06</c> or <c>07</c>.
    /// </summary>
    public string ElectronicCommerceIndicator { get; set; }

    /// <summary>
    /// Gets or sets the directory server's transaction id (<c>dsTransID</c>), or the XID of 3-D Secure 1.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the 3-D Secure version that ran, such as <c>2.2.0</c>.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the <c>transStatus</c> the issuer's access control server returned in the ARes, when it is known.
    /// </summary>
    public string TransactionStatus { get; set; }

    /// <summary>
    /// Gets a value indicating whether a payment can go ahead with it (<see cref="ThreeDSecureOutcomes.AllowsPayment"/>).
    /// </summary>
    public bool AllowsPayment
        => ThreeDSecureOutcomes.AllowsPayment(Outcome);

    /// <summary>
    /// Returns the name of the first member that isn't valid, such as <see cref="Cryptogram"/>, or
    /// <see langword="null"/> when it is valid. A result that lets the payment go ahead must carry the cryptogram, the
    /// indicator, the transaction id and the version.
    /// </summary>
    public string GetInvalidMember()
    {
        if (!ThreeDSecureOutcomes.IsKnown(Outcome))
        {
            return nameof(Outcome);
        }

        if (!AllowsPayment)
        {
            return null;
        }

        if (!IsValidCryptogram(Cryptogram))
        {
            return nameof(Cryptogram);
        }

        if (ElectronicCommerceIndicator is null || !_electronicCommerceIndicators.Contains(ElectronicCommerceIndicator))
        {
            return nameof(ElectronicCommerceIndicator);
        }

        if (string.IsNullOrWhiteSpace(TransactionId) || TransactionId.Length > MaxTransactionIdLength)
        {
            return nameof(TransactionId);
        }

        if (Version is null || !VersionPattern().IsMatch(Version))
        {
            return nameof(Version);
        }

        if (TransactionStatus is not null && !_transactionStatuses.Contains(TransactionStatus))
        {
            return nameof(TransactionStatus);
        }

        return null;
    }

    /// <summary>
    /// Returns whether a value is a cryptogram: 28 base64 characters that decode to 20 bytes.
    /// </summary>
    /// <param name="value">The value.</param>
    public static bool IsValidCryptogram(string value)
    {
        if (value is null || value.Length != CryptogramLength)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[21];

        return Convert.TryFromBase64String(value, bytes, out var written) && written == 20;
    }

    /// <summary>
    /// Adds it to provider data, under the <c>ThreeDSecure…</c> keys of <see cref="CheckoutPaymentDataKeys"/>.
    /// </summary>
    /// <param name="providerData">The provider data.</param>
    public void CopyTo(IDictionary<string, string> providerData)
    {
        ArgumentNullException.ThrowIfNull(providerData);

        Set(providerData, CheckoutPaymentDataKeys.ThreeDSecureOutcome, Outcome);
        Set(providerData, CheckoutPaymentDataKeys.ThreeDSecureCryptogram, Cryptogram);
        Set(providerData, CheckoutPaymentDataKeys.ThreeDSecureElectronicCommerceIndicator, ElectronicCommerceIndicator);
        Set(providerData, CheckoutPaymentDataKeys.ThreeDSecureTransactionId, TransactionId);
        Set(providerData, CheckoutPaymentDataKeys.ThreeDSecureVersion, Version);
        Set(providerData, CheckoutPaymentDataKeys.ThreeDSecureTransactionStatus, TransactionStatus);
    }

    /// <summary>
    /// Returns the result provider data carries, or <see langword="null"/> when it carries no outcome.
    /// </summary>
    /// <param name="providerData">The provider data, which may be <see langword="null"/>.</param>
    public static ThreeDSecureResult From(IReadOnlyDictionary<string, string> providerData)
    {
        var outcome = CheckoutPaymentDataKeys.GetValue(providerData, CheckoutPaymentDataKeys.ThreeDSecureOutcome);

        if (outcome is null)
        {
            return null;
        }

        return new ThreeDSecureResult
        {
            Outcome = outcome,
            Cryptogram = CheckoutPaymentDataKeys.GetValue(providerData, CheckoutPaymentDataKeys.ThreeDSecureCryptogram),
            ElectronicCommerceIndicator = CheckoutPaymentDataKeys.GetValue(providerData, CheckoutPaymentDataKeys.ThreeDSecureElectronicCommerceIndicator),
            TransactionId = CheckoutPaymentDataKeys.GetValue(providerData, CheckoutPaymentDataKeys.ThreeDSecureTransactionId),
            Version = CheckoutPaymentDataKeys.GetValue(providerData, CheckoutPaymentDataKeys.ThreeDSecureVersion),
            TransactionStatus = CheckoutPaymentDataKeys.GetValue(providerData, CheckoutPaymentDataKeys.ThreeDSecureTransactionStatus),
        };
    }

    private static void Set(IDictionary<string, string> providerData, string key, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            providerData.Remove(key);

            return;
        }

        providerData[key] = value;
    }

    [GeneratedRegex(@"^\d{1,2}\.\d{1,2}\.\d{1,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
