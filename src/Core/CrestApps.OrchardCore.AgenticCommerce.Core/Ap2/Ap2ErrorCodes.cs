namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// The AP2 mandate error codes, as the UCP AP2 Mandates extension names them.
/// </summary>
public static class Ap2ErrorCodes
{
    /// <summary>
    /// The mandate is missing.
    /// </summary>
    public const string MandateRequired = "mandate_required";

    /// <summary>
    /// The mandate is malformed or breaks a structural rule of the delegation chain.
    /// </summary>
    public const string MandateInvalid = "mandate_invalid";

    /// <summary>
    /// A signature, disclosure digest or hop binding (<c>sd_hash</c>) of the chain does not verify.
    /// </summary>
    public const string MandateInvalidSignature = "mandate_invalid_signature";

    /// <summary>
    /// A mandate in the chain has expired.
    /// </summary>
    public const string MandateExpired = "mandate_expired";

    /// <summary>
    /// The mandate is not for this merchant, session or checkout: wrong <c>vct</c>, audience, nonce or checkout hash,
    /// or the checkout breaks a constraint of an open mandate.
    /// </summary>
    public const string MandateScopeMismatch = "mandate_scope_mismatch";

    /// <summary>
    /// No trusted key is known for the root of the chain, or a hop has no <c>cnf.jwk</c> for the next signer.
    /// </summary>
    public const string AgentMissingKey = "agent_missing_key";

    /// <summary>
    /// The merchant's signature on the checkout does not verify.
    /// </summary>
    public const string MerchantAuthorizationInvalid = "merchant_authorization_invalid";

    /// <summary>
    /// The checkout carries no merchant authorization.
    /// </summary>
    public const string MerchantAuthorizationMissing = "merchant_authorization_missing";
}
