using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Creates and verifies HTTP message signatures (RFC 9421) with ECDSA keys, the way UCP uses them: the algorithm comes
/// from the key, no <c>alg</c> parameter is sent, and signatures are raw <c>r||s</c>.
/// </summary>
public static class HttpMessageSignatures
{
    private const string SignatureParamsComponent = "@signature-params";

    /// <summary>
    /// Builds the signature base (RFC 9421 section 2.5): one <c>"name": value</c> line per covered component, then the
    /// <c>"@signature-params"</c> line, joined by line feeds with no trailing line feed.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="coveredComponents">The component identifiers, in order: derived components (<c>@method</c>, <c>@authority</c>, <c>@path</c>, <c>@query</c>, <c>@status</c>, <c>@target-uri</c>, <c>@request-target</c>, <c>@scheme</c>) and lowercase header names.</param>
    /// <param name="signatureParamsValue">The serialized signature parameters, the value of the <c>Signature-Input</c> member.</param>
    /// <returns>The signature base.</returns>
    /// <exception cref="InvalidOperationException">A covered component is missing from the message or is repeated.</exception>
    public static string BuildSignatureBase(HttpSignatureMessage message, IReadOnlyList<string> coveredComponents, string signatureParamsValue)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(coveredComponents);
        ArgumentNullException.ThrowIfNull(signatureParamsValue);

        var builder = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var component in coveredComponents)
        {
            if (string.IsNullOrEmpty(component) || !seen.Add(component))
            {
                throw new InvalidOperationException($"The covered component '{component}' is empty or repeated.");
            }

            builder.Append('"').Append(component).Append("\": ").Append(GetComponentValue(message, component)).Append('\n');
        }

        builder.Append('"').Append(SignatureParamsComponent).Append("\": ").Append(signatureParamsValue);

        return builder.ToString();
    }

    /// <summary>
    /// Signs a message and returns the <c>Signature-Input</c> and <c>Signature</c> header values.
    /// </summary>
    /// <param name="message">The message to sign.</param>
    /// <param name="coveredComponents">The component identifiers to cover, in order.</param>
    /// <param name="keyId">The <c>keyid</c> parameter: the <c>kid</c> of the key in the signer's published keys.</param>
    /// <param name="key">The private ECDSA key (P-256, P-384 or P-521).</param>
    /// <param name="label">The signature label.</param>
    /// <param name="created">The <c>created</c> parameter in Unix seconds, or <see langword="null"/> to leave it out.</param>
    /// <returns>The header values.</returns>
    public static HttpSignature Sign(
        HttpSignatureMessage message,
        IReadOnlyList<string> coveredComponents,
        string keyId,
        ECDsa key,
        string label = "sig1",
        long? created = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(coveredComponents);
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(label);

        var curve = JwsAlgorithms.GetCurveForKeySize(key.KeySize)
            ?? throw new NotSupportedException($"An ECDSA key of {key.KeySize} bits is not supported.");
        JwsAlgorithms.TryGetAlgorithm(JwsAlgorithms.GetAlgorithmForCurve(curve), out var hashAlgorithm, out _);

        var parameters = new OrderedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["keyid"] = keyId,
        };

        if (created.HasValue)
        {
            parameters["created"] = created.Value;
        }

        var items = new List<StructuredFieldItem>(coveredComponents.Count);

        foreach (var component in coveredComponents)
        {
            items.Add(new StructuredFieldItem(component));
        }

        var signatureParams = StructuredFieldSerializer.SerializeInnerList(items, parameters);
        var signatureBase = BuildSignatureBase(message, coveredComponents, signatureParams);
        var signature = key.SignData(Encoding.UTF8.GetBytes(signatureBase), hashAlgorithm);

        return new HttpSignature(
            label + "=" + signatureParams,
            label + "=" + StructuredFieldSerializer.SerializeByteSequence(signature));
    }

    /// <summary>
    /// Verifies the HTTP message signature of a message: the first label present in both headers. The
    /// <c>Content-Digest</c> header is not checked against the body here; the caller checks it with
    /// <see cref="ContentDigest.Verify(string, ReadOnlySpan{byte})"/>.
    /// </summary>
    /// <param name="message">The received message.</param>
    /// <param name="signatureInputHeader">The <c>Signature-Input</c> header value.</param>
    /// <param name="signatureHeader">The <c>Signature</c> header value.</param>
    /// <param name="keyResolver">Returns the signer's public key for a <c>keyid</c>, or <see langword="null"/> when no key is known.</param>
    /// <returns>The verification outcome.</returns>
    public static HttpSignatureVerification Verify(
        HttpSignatureMessage message,
        string signatureInputHeader,
        string signatureHeader,
        Func<string, EcJsonWebKey> keyResolver)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(keyResolver);

        if (string.IsNullOrWhiteSpace(signatureInputHeader) || string.IsNullOrWhiteSpace(signatureHeader)
            || !StructuredFieldParser.TryParseDictionary(signatureInputHeader, out var inputs)
            || !StructuredFieldParser.TryParseDictionary(signatureHeader, out var signatures))
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.SignatureMissing, "The Signature-Input or Signature header is missing or malformed.");
        }

        string label = null;
        StructuredFieldMember input = null;
        StructuredFieldMember signatureMember = null;

        foreach (var candidate in inputs)
        {
            if (signatures.TryGetValue(candidate.Key, out signatureMember))
            {
                label = candidate.Key;
                input = candidate.Value;

                break;
            }
        }

        if (label is null)
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.SignatureMissing, "No signature label is present in both headers.");
        }

        if (!input.IsInnerList || signatureMember.IsInnerList || signatureMember.Item.Value is not byte[] signature)
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.SignatureMissing, "The signature members are malformed.", label);
        }

        var components = new List<string>(input.InnerList.Count);

        foreach (var item in input.InnerList)
        {
            if (item.Value is not string component || item.IsToken)
            {
                return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.SignatureMissing, "A covered component is not a string.", label);
            }

            components.Add(component);
        }

        var keyId = input.Parameters.TryGetValue("keyid", out var keyIdValue)
            ? keyIdValue as string
            : null;

        if (string.IsNullOrEmpty(keyId))
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.KeyNotFound, "The signature has no keyid.", label, null, components);
        }

        var jwk = keyResolver(keyId);

        if (jwk is null)
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.KeyNotFound, "No key is known for the keyid.", label, keyId, components);
        }

        var algorithm = jwk.GetAlgorithm();

        if (!JwsAlgorithms.TryGetAlgorithm(algorithm, out var hashAlgorithm, out var keySize)
            || JwsAlgorithms.GetAlgorithmForCurve(jwk.Crv) != algorithm)
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.AlgorithmUnsupported, "The key's algorithm is not supported.", label, keyId, components);
        }

        if (input.Parameters.TryGetValue("alg", out var algValue) && !MatchesHttpAlgorithm(algValue as string, algorithm))
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.AlgorithmUnsupported, "The alg parameter does not match a supported key algorithm.", label, keyId, components);
        }

        string signatureBase;

        try
        {
            foreach (var item in input.InnerList)
            {
                if (item.Parameters.Count > 0)
                {
                    throw new InvalidOperationException("Component parameters are not supported.");
                }
            }

            signatureBase = BuildSignatureBase(message, components, StructuredFieldSerializer.SerializeMember(input));
        }
        catch (InvalidOperationException exception)
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.SignatureInvalid, exception.Message, label, keyId, components);
        }

        if (signature.Length != JwsAlgorithms.GetSignatureSize(keySize))
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.SignatureInvalid, "The signature has the wrong length.", label, keyId, components);
        }

        bool valid;

        try
        {
            using var key = jwk.CreateECDsa();
            valid = key.VerifyData(Encoding.UTF8.GetBytes(signatureBase), signature, hashAlgorithm);
        }
        catch (CryptographicException)
        {
            valid = false;
        }

        if (!valid)
        {
            return HttpSignatureVerification.Fail(HttpSignatureErrorCodes.SignatureInvalid, "The signature does not verify.", label, keyId, components);
        }

        return new HttpSignatureVerification
        {
            Succeeded = true,
            KeyId = keyId,
            Label = label,
            CoveredComponents = components,
            Created = ReadTime(input.Parameters, "created"),
            Expires = ReadTime(input.Parameters, "expires"),
            Nonce = input.Parameters.TryGetValue("nonce", out var nonce) ? nonce as string : null,
        };
    }

    // A signature parameter that is a time in Unix seconds (RFC 9421 created and expires).
    private static DateTimeOffset? ReadTime(IReadOnlyDictionary<string, object> parameters, string name)
        => parameters.TryGetValue(name, out var value) && value is not null && long.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;

    /// <summary>
    /// Returns the components a UCP request signature covers (UCP signatures specification): <c>@method</c>,
    /// <c>@authority</c>, <c>@path</c>, then <c>@query</c>, <c>ucp-agent</c> and <c>idempotency-key</c> when present,
    /// then <c>content-digest</c> and <c>content-type</c> when there is a body.
    /// </summary>
    /// <param name="hasBody">Whether the request has a body.</param>
    /// <param name="hasQuery">Whether the target URI has a query.</param>
    /// <param name="hasUcpAgent">Whether the request has a <c>UCP-Agent</c> header.</param>
    /// <param name="hasIdempotencyKey">Whether the request has an <c>Idempotency-Key</c> header.</param>
    /// <returns>The component identifiers, in order.</returns>
    public static IReadOnlyList<string> DefaultRequestComponents(bool hasBody, bool hasQuery, bool hasUcpAgent, bool hasIdempotencyKey)
    {
        var components = new List<string> { "@method", "@authority", "@path" };

        if (hasQuery)
        {
            components.Add("@query");
        }

        if (hasUcpAgent)
        {
            components.Add("ucp-agent");
        }

        if (hasIdempotencyKey)
        {
            components.Add("idempotency-key");
        }

        if (hasBody)
        {
            components.Add("content-digest");
            components.Add("content-type");
        }

        return components;
    }

    /// <summary>
    /// Returns the components a UCP response signature covers: <c>@status</c>, then <c>content-digest</c> and
    /// <c>content-type</c> when there is a body.
    /// </summary>
    /// <param name="hasBody">Whether the response has a body.</param>
    /// <returns>The component identifiers, in order.</returns>
    public static IReadOnlyList<string> DefaultResponseComponents(bool hasBody)
    {
        var components = new List<string> { "@status" };

        if (hasBody)
        {
            components.Add("content-digest");
            components.Add("content-type");
        }

        return components;
    }

    private static string GetComponentValue(HttpSignatureMessage message, string component)
    {
        switch (component)
        {
            case "@method":
                return Require(message.Method, component);

            case "@authority":
                return Require(message.Authority, component).ToLowerInvariant();

            case "@scheme":
                return Require(message.Scheme, component).ToLowerInvariant();

            case "@path":
                return GetPath(message);

            case "@query":
                return GetQuery(message);

            case "@request-target":
                return GetPath(message) + (string.IsNullOrEmpty(message.Query) ? string.Empty : GetQuery(message));

            case "@target-uri":
                return Require(message.Scheme, component).ToLowerInvariant() + "://" + Require(message.Authority, component).ToLowerInvariant()
                    + GetPath(message) + (string.IsNullOrEmpty(message.Query) ? string.Empty : GetQuery(message));

            case "@status":
                return message.Status.HasValue
                    ? message.Status.Value.ToString("D3", CultureInfo.InvariantCulture)
                    : throw new InvalidOperationException("The message has no status for the '@status' component.");
        }

        if (component[0] == '@')
        {
            throw new InvalidOperationException($"The derived component '{component}' is not supported.");
        }

        if (!string.Equals(component, component.ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The header component '{component}' must be lowercase.");
        }

        if (!message.TryGetHeader(component, out var value))
        {
            throw new InvalidOperationException($"The message has no '{component}' header.");
        }

        if (value.Contains('\r') || value.Contains('\n'))
        {
            throw new InvalidOperationException($"The '{component}' header holds a line break.");
        }

        return value.Trim(' ', '\t');
    }

    private static string GetPath(HttpSignatureMessage message)
    {
        return string.IsNullOrEmpty(message.Path)
            ? "/"
            : message.Path;
    }

    private static string GetQuery(HttpSignatureMessage message)
    {
        if (string.IsNullOrEmpty(message.Query))
        {
            return "?";
        }

        return message.Query[0] == '?'
            ? message.Query
            : "?" + message.Query;
    }

    private static string Require(string value, string component)
    {
        return string.IsNullOrEmpty(value)
            ? throw new InvalidOperationException($"The message has no value for the '{component}' component.")
            : value;
    }

    private static bool MatchesHttpAlgorithm(string httpAlgorithm, string jwsAlgorithm)
    {
        return (httpAlgorithm, jwsAlgorithm) switch
        {
            ("ecdsa-p256-sha256", JwsAlgorithms.ES256) => true,
            ("ecdsa-p384-sha384", JwsAlgorithms.ES384) => true,
            _ => false,
        };
    }
}
