namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The outcomes of a 3-D Secure authentication run outside the payment provider (<see cref="ThreeDSecureResult.Outcome"/>).
/// </summary>
public static class ThreeDSecureOutcomes
{
    /// <summary>
    /// The cardholder was authenticated.
    /// </summary>
    public const string Authenticated = "authenticated";

    /// <summary>
    /// The issuer couldn't authenticate the cardholder but acknowledged the attempt, which still carries a cryptogram.
    /// </summary>
    public const string AttemptAcknowledged = "attempt_acknowledged";

    /// <summary>
    /// The issuer answered for information only, with a cryptogram the payment can go ahead with.
    /// </summary>
    public const string Informational = "informational";

    /// <summary>
    /// The issuer refused to authenticate the cardholder.
    /// </summary>
    public const string Denied = "denied";

    /// <summary>
    /// The issuer rejected the authentication and the payment must not be tried.
    /// </summary>
    public const string Rejected = "rejected";

    /// <summary>
    /// The cardholder left the authentication before it finished.
    /// </summary>
    public const string Abandoned = "abandoned";

    /// <summary>
    /// The authentication was canceled.
    /// </summary>
    public const string Canceled = "canceled";

    /// <summary>
    /// The authentication service failed.
    /// </summary>
    public const string InternalError = "internal_error";

    /// <summary>
    /// The card or its issuer doesn't support 3-D Secure.
    /// </summary>
    public const string NotSupported = "not_supported";

    /// <summary>
    /// The authentication couldn't be processed.
    /// </summary>
    public const string ProcessingError = "processing_error";

    private static readonly HashSet<string> _all = new(StringComparer.Ordinal)
    {
        Authenticated,
        AttemptAcknowledged,
        Informational,
        Denied,
        Rejected,
        Abandoned,
        Canceled,
        InternalError,
        NotSupported,
        ProcessingError,
    };

    /// <summary>
    /// Returns whether a payment can go ahead with the outcome: the issuer gave a cryptogram to charge with.
    /// </summary>
    /// <param name="outcome">The outcome.</param>
    public static bool AllowsPayment(string outcome)
        => outcome is Authenticated or AttemptAcknowledged or Informational;

    /// <summary>
    /// Returns whether the outcome is one of these.
    /// </summary>
    /// <param name="outcome">The outcome.</param>
    public static bool IsKnown(string outcome)
        => outcome is not null && _all.Contains(outcome);
}
