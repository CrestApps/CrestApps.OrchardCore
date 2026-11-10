using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// An SD-JWT disclosure (RFC 9901 section 4.2): the base64url of <c>[salt, name, value]</c> for an object property
/// or <c>[salt, value]</c> for an array element.
/// </summary>
public sealed class SdJwtDisclosure
{
    private SdJwtDisclosure(string encoded, string salt, string claimName, JsonNode value)
    {
        Encoded = encoded;
        Salt = salt;
        ClaimName = claimName;
        Value = value;
        Digest = Ap2Hashing.Hash(encoded, Ap2Hashing.Sha256);
    }

    /// <summary>
    /// Gets the disclosure exactly as it appears in the SD-JWT.
    /// </summary>
    public string Encoded { get; }

    /// <summary>
    /// Gets the salt.
    /// </summary>
    public string Salt { get; }

    /// <summary>
    /// Gets the claim name, or <see langword="null"/> for an array element disclosure.
    /// </summary>
    public string ClaimName { get; }

    /// <summary>
    /// Gets the disclosed value.
    /// </summary>
    public JsonNode Value { get; }

    /// <summary>
    /// Gets the SHA-256 digest of the disclosure: base64url(sha256(ASCII(<see cref="Encoded"/>))).
    /// </summary>
    public string Digest { get; }

    /// <summary>
    /// Creates a disclosure, as an issuer or holder does when it issues an SD-JWT.
    /// </summary>
    /// <param name="salt">A random salt with at least 128 bits of entropy.</param>
    /// <param name="claimName">The claim name, or <see langword="null"/> for an array element disclosure.</param>
    /// <param name="value">The disclosed value.</param>
    /// <returns>The disclosure.</returns>
    public static SdJwtDisclosure Create(string salt, string claimName, JsonNode value)
    {
        ArgumentException.ThrowIfNullOrEmpty(salt);

        var array = new JsonArray { salt };

        if (claimName is not null)
        {
            array.Add(claimName);
        }

        array.Add(value?.DeepClone());

        var encoded = JoseJson.EncodeSegment(array);

        return new SdJwtDisclosure(encoded, salt, claimName, value?.DeepClone());
    }

    /// <summary>
    /// Reads a disclosure.
    /// </summary>
    /// <param name="encoded">The base64url disclosure.</param>
    /// <param name="disclosure">The disclosure when it is valid.</param>
    /// <returns><see langword="true"/> when the value decodes to a JSON array of a string salt with a value, or with a string claim name and a value.</returns>
    public static bool TryParse(string encoded, out SdJwtDisclosure disclosure)
    {
        disclosure = null;

        if (string.IsNullOrEmpty(encoded)
            || !JoseJson.TryDecodeBase64Url(encoded, out var bytes)
            || !JoseJson.TryParseNode(bytes, out var node)
            || node is not JsonArray array
            || array.Count is not (2 or 3)
            || !JoseJson.IsJsonString(array[0], out var salt))
        {
            return false;
        }

        string claimName = null;

        if (array.Count == 3 && !JoseJson.IsJsonString(array[1], out claimName))
        {
            return false;
        }

        disclosure = new SdJwtDisclosure(encoded, salt, claimName, array[array.Count - 1]?.DeepClone());

        return true;
    }

    internal string ComputeDigest(string sdAlg)
    {
        return sdAlg is null or Ap2Hashing.Sha256
            ? Digest
            : Ap2Hashing.Hash(Encoded, sdAlg);
    }
}
