using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// A selective disclosure JWT (SD-JWT, RFC 9901) in compact form:
/// <c>&lt;issuer JWT&gt;~&lt;disclosure&gt;~...~[&lt;key binding JWT&gt;]</c>. Parsing does not verify signatures.
/// </summary>
public sealed class SdJwt
{
    private const string SdClaim = "_sd";

    private const string SdAlgClaim = "_sd_alg";

    private const string ArrayDigestKey = "...";

    private SdJwt()
    {
    }

    /// <summary>
    /// Gets the issuer-signed JWT.
    /// </summary>
    public string IssuerJwt { get; private init; }

    /// <summary>
    /// Gets the disclosures, in their order in the SD-JWT.
    /// </summary>
    public IReadOnlyList<SdJwtDisclosure> Disclosures { get; private init; }

    /// <summary>
    /// Gets the key binding JWT, or <see langword="null"/> when the SD-JWT has none.
    /// </summary>
    public string KeyBindingJwt { get; private init; }

    /// <summary>
    /// Gets the decoded header of the issuer-signed JWT.
    /// </summary>
    public JsonObject IssuerHeader { get; private init; }

    /// <summary>
    /// Gets the decoded payload of the issuer-signed JWT, with its digests unresolved.
    /// </summary>
    public JsonObject IssuerPayload { get; private init; }

    /// <summary>
    /// Gets the <c>typ</c> header of the issuer-signed JWT.
    /// </summary>
    public string Type => JoseJson.GetString(IssuerHeader, "typ");

    /// <summary>
    /// Gets the hash algorithm of the digests (<c>_sd_alg</c>), <c>sha-256</c> when the payload does not name one.
    /// </summary>
    public string SdAlg => JoseJson.GetString(IssuerPayload, SdAlgClaim) ?? Ap2Hashing.Sha256;

    /// <summary>
    /// Gets the SD-JWT without its key binding JWT: <c>&lt;issuer JWT&gt;~&lt;disclosure&gt;~...~</c>, the input of <c>sd_hash</c>.
    /// </summary>
    public string SdJwtWithoutKeyBinding
    {
        get
        {
            var builder = new StringBuilder(IssuerJwt).Append('~');

            foreach (var disclosure in Disclosures)
            {
                builder.Append(disclosure.Encoded).Append('~');
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Parses a compact SD-JWT.
    /// </summary>
    /// <param name="compact">The compact SD-JWT.</param>
    /// <param name="sdJwt">The parsed SD-JWT when it is well formed.</param>
    /// <returns><see langword="true"/> when the issuer JWT has a JSON object header and payload and every disclosure is valid.</returns>
    public static bool TryParse(string compact, out SdJwt sdJwt)
    {
        sdJwt = null;

        if (string.IsNullOrEmpty(compact) || compact[0] == '~' || !compact.Contains('~'))
        {
            return false;
        }

        var parts = compact.Split('~');
        var jwtParts = parts[0].Split('.');

        if (jwtParts.Length != 3
            || !JoseJson.TryDecodeObjectSegment(jwtParts[0], out var header)
            || !JoseJson.TryDecodeObjectSegment(jwtParts[1], out var payload))
        {
            return false;
        }

        var disclosures = new List<SdJwtDisclosure>(parts.Length - 2);

        for (var i = 1; i < parts.Length - 1; i++)
        {
            if (!SdJwtDisclosure.TryParse(parts[i], out var disclosure))
            {
                return false;
            }

            disclosures.Add(disclosure);
        }

        var keyBinding = parts[parts.Length - 1];

        if (keyBinding.Length > 0 && keyBinding.Split('.').Length != 3)
        {
            return false;
        }

        sdJwt = new SdJwt
        {
            IssuerJwt = parts[0],
            Disclosures = disclosures,
            KeyBindingJwt = keyBinding.Length > 0
                ? keyBinding
                : null,
            IssuerHeader = header,
            IssuerPayload = payload,
        };

        return true;
    }

    /// <summary>
    /// Resolves the disclosed claims (RFC 9901 section 7.1): replaces <c>_sd</c> digests with the disclosed
    /// properties and <c>{"...": digest}</c> array elements with the disclosed values, recursively, drops undisclosed
    /// digests, and removes <c>_sd</c> and <c>_sd_alg</c>.
    /// </summary>
    /// <returns>The resolved claims.</returns>
    /// <exception cref="FormatException">The SD-JWT is not valid: a disclosure is unreferenced or referenced twice, a digest is malformed, or a disclosed property already exists.</exception>
    public JsonObject ResolveClaims()
    {
        if (!TryResolveClaims(out var claims, out var error))
        {
            throw new FormatException(error);
        }

        return claims;
    }

    /// <summary>
    /// Resolves the disclosed claims like <see cref="ResolveClaims"/> and reports a failure instead of throwing.
    /// </summary>
    /// <param name="claims">The resolved claims when the SD-JWT is valid.</param>
    /// <param name="error">The reason when the SD-JWT is not valid.</param>
    /// <returns><see langword="true"/> when the claims were resolved.</returns>
    public bool TryResolveClaims(out JsonObject claims, out string error)
    {
        return TryResolveClaims(false, out claims, out error, out _);
    }

    /// <summary>
    /// Computes <c>sd_hash</c> of this SD-JWT: the hash of <see cref="SdJwtWithoutKeyBinding"/> with <see cref="SdAlg"/>.
    /// </summary>
    /// <returns>The base64url hash.</returns>
    public string ComputeSdHash()
    {
        return Ap2Hashing.Hash(SdJwtWithoutKeyBinding, SdAlg);
    }

    /// <summary>
    /// Computes <c>issuer_jwt_hash</c> of this SD-JWT: the hash of <see cref="IssuerJwt"/> with <see cref="SdAlg"/>.
    /// </summary>
    /// <returns>The base64url hash.</returns>
    public string ComputeIssuerJwtHash()
    {
        return Ap2Hashing.Hash(IssuerJwt, SdAlg);
    }

    internal bool TryResolveClaims(
        bool resolveDelegatePayloadDigests,
        out JsonObject claims,
        out string error,
        out bool digestMismatch)
    {
        claims = null;
        digestMismatch = false;

        var sdAlg = JoseJson.GetString(IssuerPayload, SdAlgClaim);

        if (IssuerPayload.ContainsKey(SdAlgClaim) && (sdAlg is null || !Ap2Hashing.IsSupported(sdAlg)))
        {
            error = "The _sd_alg hash algorithm is not supported.";

            return false;
        }

        var resolver = new Resolver(this, sdAlg);

        if (!resolver.TryIndex(out error))
        {
            return false;
        }

        var payload = IssuerPayload.DeepClone().AsObject();

        if (resolveDelegatePayloadDigests && payload["delegate_payload"] is JsonArray delegatePayload)
        {
            resolver.ResolveBareDigests(delegatePayload);
        }

        if (!resolver.TryProcessObject(payload, out error))
        {
            return false;
        }

        payload.Remove(SdAlgClaim);

        if (resolver.UsedCount != Disclosures.Count)
        {
            digestMismatch = true;
            error = "A disclosure is not referenced by any digest in the SD-JWT.";

            return false;
        }

        claims = payload;
        error = null;

        return true;
    }

    private sealed class Resolver
    {
        private readonly SdJwt _sdJwt;
        private readonly string _sdAlg;
        private readonly Dictionary<string, SdJwtDisclosure> _byDigest = new(StringComparer.Ordinal);
        private readonly HashSet<string> _used = new(StringComparer.Ordinal);

        public Resolver(SdJwt sdJwt, string sdAlg)
        {
            _sdJwt = sdJwt;
            _sdAlg = sdAlg;
        }

        public int UsedCount => _used.Count;

        public bool TryIndex(out string error)
        {
            foreach (var disclosure in _sdJwt.Disclosures)
            {
                if (!_byDigest.TryAdd(disclosure.ComputeDigest(_sdAlg), disclosure))
                {
                    error = "The SD-JWT repeats a disclosure.";

                    return false;
                }
            }

            error = null;

            return true;
        }

        public void ResolveBareDigests(JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (JoseJson.IsJsonString(array[i], out var digest)
                    && _byDigest.TryGetValue(digest, out var disclosure)
                    && _used.Add(digest))
                {
                    array[i] = disclosure.Value?.DeepClone();
                }
            }
        }

        public bool TryProcessObject(JsonObject obj, out string error)
        {
            if (obj.TryGetPropertyValue(SdClaim, out var sdNode))
            {
                if (sdNode is not JsonArray digests)
                {
                    error = "An _sd claim is not an array.";

                    return false;
                }

                obj.Remove(SdClaim);

                foreach (var digestNode in digests)
                {
                    if (!JoseJson.IsJsonString(digestNode, out var digest))
                    {
                        error = "An _sd digest is not a string.";

                        return false;
                    }

                    if (!_byDigest.TryGetValue(digest, out var disclosure))
                    {
                        continue;
                    }

                    if (!_used.Add(digest))
                    {
                        error = "A digest is referenced more than once.";

                        return false;
                    }

                    if (disclosure.ClaimName is null || disclosure.ClaimName is SdClaim or ArrayDigestKey)
                    {
                        error = "An _sd digest references a disclosure that is not an object property.";

                        return false;
                    }

                    if (obj.ContainsKey(disclosure.ClaimName))
                    {
                        error = $"The disclosed claim '{disclosure.ClaimName}' already exists.";

                        return false;
                    }

                    obj[disclosure.ClaimName] = disclosure.Value?.DeepClone();
                }
            }

            var names = new List<string>(obj.Count);

            foreach (var property in obj)
            {
                names.Add(property.Key);
            }

            foreach (var name in names)
            {
                if (!TryProcessNode(obj[name], out error))
                {
                    return false;
                }
            }

            error = null;

            return true;
        }

        private bool TryProcessArray(JsonArray array, out string error)
        {
            var i = 0;

            while (i < array.Count)
            {
                if (array[i] is JsonObject element && element.Count == 1 && element.TryGetPropertyValue(ArrayDigestKey, out var digestNode))
                {
                    if (!JoseJson.IsJsonString(digestNode, out var digest))
                    {
                        error = "An array element digest is not a string.";

                        return false;
                    }

                    if (!_byDigest.TryGetValue(digest, out var disclosure))
                    {
                        array.RemoveAt(i);

                        continue;
                    }

                    if (!_used.Add(digest))
                    {
                        error = "A digest is referenced more than once.";

                        return false;
                    }

                    if (disclosure.ClaimName is not null)
                    {
                        error = "An array element digest references an object property disclosure.";

                        return false;
                    }

                    array[i] = disclosure.Value?.DeepClone();
                }

                if (!TryProcessNode(array[i], out error))
                {
                    return false;
                }

                i++;
            }

            error = null;

            return true;
        }

        private bool TryProcessNode(JsonNode node, out string error)
        {
            error = null;

            return node switch
            {
                JsonObject obj => TryProcessObject(obj, out error),
                JsonArray array => TryProcessArray(array, out error),
                _ => true,
            };
        }
    }
}
