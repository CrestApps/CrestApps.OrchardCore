using System.Security.Cryptography;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Computes and verifies the <c>Content-Digest</c> HTTP header (RFC 9530) over the raw body bytes.
/// </summary>
public static class ContentDigest
{
    /// <summary>
    /// Computes the <c>Content-Digest</c> value <c>sha-256=:&lt;base64&gt;:</c> of a body.
    /// </summary>
    /// <param name="body">The raw body bytes, exactly as sent.</param>
    /// <returns>The header value.</returns>
    public static string ComputeSha256(ReadOnlySpan<byte> body)
    {
        return "sha-256=:" + Convert.ToBase64String(SHA256.HashData(body)) + ":";
    }

    /// <summary>
    /// Verifies a <c>Content-Digest</c> value against a body. The <c>sha-256</c> and <c>sha-512</c> members are
    /// checked, and members with other algorithms are ignored.
    /// </summary>
    /// <param name="headerValue">The <c>Content-Digest</c> header value.</param>
    /// <param name="body">The raw body bytes, exactly as received.</param>
    /// <returns><see langword="true"/> when at least one supported member is present and every supported member matches.</returns>
    public static bool Verify(string headerValue, ReadOnlySpan<byte> body)
    {
        if (string.IsNullOrWhiteSpace(headerValue) || !StructuredFieldParser.TryParseDictionary(headerValue, out var dictionary))
        {
            return false;
        }

        var checkedAny = false;

        foreach (var member in dictionary)
        {
            byte[] expected;

            switch (member.Key)
            {
                case "sha-256":
                    expected = SHA256.HashData(body);
                    break;

                case "sha-512":
                    expected = SHA512.HashData(body);
                    break;

                default:
                    continue;
            }

            if (member.Value.IsInnerList || member.Value.Item.Value is not byte[] actual)
            {
                return false;
            }

            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                return false;
            }

            checkedAny = true;
        }

        return checkedAny;
    }
}
