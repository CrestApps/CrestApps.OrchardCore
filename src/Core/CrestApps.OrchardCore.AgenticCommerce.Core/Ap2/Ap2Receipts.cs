using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// Creates AP2 checkout receipts: ES256 JWTs the merchant returns for a checkout mandate, on success and on error.
/// </summary>
public static class Ap2Receipts
{
    /// <summary>
    /// The receipt status of an accepted mandate.
    /// </summary>
    public const string StatusSuccess = "Success";

    /// <summary>
    /// The receipt status of a rejected mandate.
    /// </summary>
    public const string StatusError = "Error";

    /// <summary>
    /// Creates a checkout receipt with the claims <c>status</c>, <c>iss</c>, <c>iat</c>, <c>reference</c> and, for an
    /// error, <c>error</c>.
    /// </summary>
    /// <param name="reference">The hash of the closed checkout mandate (<see cref="Ap2MandateVerificationResult.ClosedMandateHash"/>).</param>
    /// <param name="status"><see cref="StatusSuccess"/> or <see cref="StatusError"/>.</param>
    /// <param name="merchantIssuer">The merchant issuer, such as the store's website URL.</param>
    /// <param name="issuedAt">The issue time.</param>
    /// <param name="key">The merchant's P-256 private signing key.</param>
    /// <param name="errorCode">The AP2 receipt error (<c>invalid_credential</c>, <c>unresolved_constraint</c>, <c>invalid_mandate</c>, <c>mandates_not_supported</c>) when the status is an error.</param>
    /// <returns>The compact receipt JWT.</returns>
    public static string CreateCheckoutReceipt(
        string reference,
        string status,
        string merchantIssuer,
        DateTimeOffset issuedAt,
        EcJsonWebKey key,
        string errorCode = null)
    {
        return CreateCheckoutReceipt(reference, status, merchantIssuer, issuedAt, key, errorCode, null, null);
    }

    /// <summary>
    /// Creates a checkout receipt with the claims <c>status</c>, <c>iss</c>, <c>iat</c>, <c>reference</c>, and
    /// <c>order_id</c> on success or <c>error</c> and <c>error_description</c> on error.
    /// </summary>
    /// <param name="reference">The hash of the closed checkout mandate (<see cref="Ap2MandateVerificationResult.ClosedMandateHash"/>).</param>
    /// <param name="status"><see cref="StatusSuccess"/> or <see cref="StatusError"/>.</param>
    /// <param name="merchantIssuer">The merchant issuer, such as the store's website URL.</param>
    /// <param name="issuedAt">The issue time.</param>
    /// <param name="key">The merchant's P-256 private signing key.</param>
    /// <param name="errorCode">The AP2 receipt error when the status is an error, or <see langword="null"/>.</param>
    /// <param name="errorDescription">A human-readable error description, or <see langword="null"/>.</param>
    /// <param name="orderId">The order ID when the status is a success, or <see langword="null"/>.</param>
    /// <returns>The compact receipt JWT.</returns>
    public static string CreateCheckoutReceipt(
        string reference,
        string status,
        string merchantIssuer,
        DateTimeOffset issuedAt,
        EcJsonWebKey key,
        string errorCode,
        string errorDescription,
        string orderId)
    {
        ArgumentException.ThrowIfNullOrEmpty(reference);
        ArgumentException.ThrowIfNullOrEmpty(status);
        ArgumentException.ThrowIfNullOrEmpty(merchantIssuer);
        ArgumentNullException.ThrowIfNull(key);

        var claims = new JsonObject
        {
            ["status"] = status,
            ["iss"] = merchantIssuer,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["reference"] = reference,
        };

        if (!string.IsNullOrEmpty(orderId))
        {
            claims["order_id"] = orderId;
        }

        if (!string.IsNullOrEmpty(errorCode))
        {
            claims["error"] = errorCode;
        }

        if (!string.IsNullOrEmpty(errorDescription))
        {
            claims["error_description"] = errorDescription;
        }

        return JwtEs256.Create(claims, key);
    }
}
