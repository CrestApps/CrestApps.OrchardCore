using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Validates Telnyx webhook signatures. Telnyx signs every webhook delivery with Ed25519 over
/// <c>{timestamp}|{raw_body}</c> and delivers the base64 signature in the <c>telnyx-signature-ed25519</c>
/// header and the Unix-second timestamp in the <c>telnyx-timestamp</c> header. The signature is verified
/// against the account public key configured in the tenant settings.
/// </summary>
public static class TelnyxWebhookSignatureValidator
{
    /// <summary>
    /// Verifies the Ed25519 signature of a Telnyx webhook delivery.
    /// </summary>
    /// <param name="publicKeyBase64">The Telnyx account Ed25519 public key (base64).</param>
    /// <param name="signatureBase64">The value of the <c>telnyx-signature-ed25519</c> header.</param>
    /// <param name="timestamp">The value of the <c>telnyx-timestamp</c> header.</param>
    /// <param name="rawBody">The raw request body exactly as received.</param>
    /// <returns><see langword="true"/> when the signature is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryValidate(string publicKeyBase64, string signatureBase64, string timestamp, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(publicKeyBase64) ||
            string.IsNullOrWhiteSpace(signatureBase64) ||
            string.IsNullOrWhiteSpace(timestamp) ||
            rawBody is null)
        {
            return false;
        }

        if (!TryDecodeBase64(publicKeyBase64, out var publicKeyBytes) ||
            publicKeyBytes.Length != Ed25519PublicKeyParameters.KeySize ||
            !TryDecodeBase64(signatureBase64, out var signatureBytes))
        {
            return false;
        }

        var signedPayload = Encoding.UTF8.GetBytes($"{timestamp}|{rawBody}");

        try
        {
            var publicKey = new Ed25519PublicKeyParameters(publicKeyBytes, 0);
            var verifier = new Ed25519Signer();
            verifier.Init(false, publicKey);
            verifier.BlockUpdate(signedPayload, 0, signedPayload.Length);

            return verifier.VerifySignature(signatureBytes);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value.Trim());

            return true;
        }
        catch (FormatException)
        {
            bytes = null;

            return false;
        }
    }
}
