using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// The outcome of verifying an AP2 checkout mandate.
/// </summary>
public sealed class Ap2MandateVerificationResult
{
    /// <summary>
    /// Gets a value indicating whether the mandate verified.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the error code when the mandate did not verify; one of <see cref="Ap2ErrorCodes"/>.
    /// </summary>
    public string ErrorCode { get; init; }

    /// <summary>
    /// Gets a description of the failure, for logs.
    /// </summary>
    public string ErrorMessage { get; init; }

    /// <summary>
    /// Gets the merchant-signed checkout JWT carried by the closed mandate (<c>checkout_jwt</c>).
    /// </summary>
    public string CheckoutJwt { get; init; }

    /// <summary>
    /// Gets the <c>checkout_hash</c> of the closed mandate.
    /// </summary>
    public string CheckoutHash { get; init; }

    /// <summary>
    /// Gets the decoded payload of <see cref="CheckoutJwt"/>: the checkout the user or agent agreed to.
    /// </summary>
    public JsonObject Checkout { get; init; }

    /// <summary>
    /// Gets the resolved content of the closed checkout mandate (<c>vct</c>, <c>checkout_jwt</c>, <c>checkout_hash</c>, ...).
    /// </summary>
    public JsonObject ClosedMandate { get; init; }

    /// <summary>
    /// Gets the hash of the closed mandate, the <c>reference</c> of a checkout receipt: base64url(sha256(ASCII(closed
    /// hop JWT))), without disclosures, as the AP2 SDK computes it.
    /// </summary>
    public string ClosedMandateHash { get; init; }

    /// <summary>
    /// Gets the <c>sd_hash</c> of the root (open) mandate with its disclosures, or <see langword="null"/> when the
    /// chain has a single token.
    /// </summary>
    public string OpenMandateHash { get; init; }

    /// <summary>
    /// Gets the resolved content of the open mandate closest to the closed one (its <c>vct</c>, <c>constraints</c>
    /// array and <c>cnf</c>), or <see langword="null"/> when the chain has no open mandate.
    /// </summary>
    public JsonObject Constraints { get; init; }

    internal static Ap2MandateVerificationResult Fail(string errorCode, string errorMessage)
    {
        return new Ap2MandateVerificationResult
        {
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };
    }
}
