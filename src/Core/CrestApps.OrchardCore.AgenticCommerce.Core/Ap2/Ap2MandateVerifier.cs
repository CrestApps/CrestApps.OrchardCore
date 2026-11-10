using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// Verifies AP2 v0.2 checkout mandates the way the AP2 SDK does: a delegation chain of SD-JWTs joined by <c>~~</c>,
/// where the root is signed by a trusted issuer or agent provider key, each later hop is signed by the key in the
/// previous hop's <c>cnf.jwk</c> and bound to it by <c>sd_hash</c> (or <c>issuer_jwt_hash</c>), and the terminal hop
/// (<c>typ: kb+sd-jwt</c>) carries <c>iat</c>, <c>aud</c> and <c>nonce</c> and the closed mandate
/// (<c>vct: mandate.checkout.1</c>). A single root SD-JWT with a closed mandate and a key binding JWT is accepted too.
/// </summary>
public sealed class Ap2MandateVerifier
{
    /// <summary>
    /// Computes the <c>checkout_hash</c> of a checkout JWT: base64url(sha256(ASCII(checkoutJwt))).
    /// </summary>
    /// <param name="checkoutJwt">The compact checkout JWT.</param>
    /// <returns>The base64url hash.</returns>
    public static string ComputeCheckoutHash(string checkoutJwt)
    {
        ArgumentNullException.ThrowIfNull(checkoutJwt);

        return Ap2Hashing.Hash(checkoutJwt, Ap2Hashing.Sha256);
    }

    /// <summary>
    /// Verifies a checkout mandate.
    /// </summary>
    /// <param name="mandate">The mandate: a <c>~~</c>-joined delegation chain, or a single SD-JWT with a key binding JWT.</param>
    /// <param name="options">The trusted keys, expected audience and nonce, and the current time.</param>
    /// <returns>The outcome, with the checkout the mandate authorizes when it verified.</returns>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "An instance service, so it can be registered and replaced.")]
    public Ap2MandateVerificationResult VerifyCheckoutMandate(string mandate, Ap2VerificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(mandate))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateRequired, "The mandate is missing.");
        }

        var segments = mandate.Split("~~");
        var tokens = new List<SdJwt>(segments.Length);

        for (var i = 0; i < segments.Length; i++)
        {
            var isLast = i == segments.Length - 1;

            if (!SdJwt.TryParse(CanonicalSegment(segments[i], isLast), out var token))
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"Token {i} of the mandate is malformed.");
            }

            if (!isLast && token.KeyBindingJwt is not null)
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"Token {i} carries a key binding JWT but is not the last token.");
            }

            tokens.Add(token);
        }

        var context = new VerificationContext(options);
        var failure = ResolveRootKey(tokens[0], options, out var currentKey);

        if (failure is not null)
        {
            return failure;
        }

        var hops = new List<VerifiedHop>(tokens.Count);

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var isLast = i == tokens.Count - 1;
            var isTerminal = false;

            if (i > 0)
            {
                isTerminal = Ap2MandateTypes.IsTerminalHop(token.Type);

                if (!isTerminal && !Ap2MandateTypes.IsIntermediateHop(token.Type))
                {
                    return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"Token {i} has the unexpected typ '{token.Type}'.");
                }

                currentKey = hops[i - 1].FindConfirmationKey();

                if (currentKey is null)
                {
                    return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.AgentMissingKey, $"Token {i - 1} has no cnf.jwk for the next signer.");
                }
            }

            if (!VerifyIssuerSignature(token, currentKey))
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalidSignature, $"The signature of token {i} does not verify.");
            }

            if (!token.TryResolveClaims(true, out var payload, out var error, out var digestMismatch))
            {
                return Ap2MandateVerificationResult.Fail(digestMismatch ? Ap2ErrorCodes.MandateInvalidSignature : Ap2ErrorCodes.MandateInvalid, $"Token {i}: {error}");
            }

            var hop = new VerifiedHop(token, payload);

            if (i > 0)
            {
                failure = VerifyHopClaims(i, hop, tokens[i - 1], isTerminal, isLast, context);

                if (failure is not null)
                {
                    return failure;
                }
            }

            if (isLast && tokens.Count > 1 && !isTerminal)
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, "The last token of the chain is not a terminal (kb+sd-jwt) hop.");
            }

            if (!isLast && hop.Items.Count != 1)
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"Token {i} discloses {hop.Items.Count} delegate payload items, expected exactly 1.");
            }

            failure = context.CheckTimes(i, hop);

            if (failure is not null)
            {
                return failure;
            }

            hops.Add(hop);
        }

        if (tokens.Count == 1)
        {
            failure = VerifyKeyBinding(hops[0], context);

            if (failure is not null)
            {
                return failure;
            }
        }

        return VerifyCheckout(hops, options);
    }

    private static Ap2MandateVerificationResult VerifyHopClaims(
        int index,
        VerifiedHop hop,
        SdJwt previous,
        bool isTerminal,
        bool isLast,
        VerificationContext context)
    {
        var payload = hop.Payload;
        var hasSdHash = payload.ContainsKey("sd_hash");
        var hasIssuerJwtHash = payload.ContainsKey("issuer_jwt_hash");

        if (hasSdHash == hasIssuerJwtHash)
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"Token {index} must carry exactly one of sd_hash and issuer_jwt_hash.");
        }

        var expectedBinding = hasSdHash
            ? previous.ComputeSdHash()
            : previous.ComputeIssuerJwtHash();
        var actualBinding = JoseJson.GetString(payload, hasSdHash ? "sd_hash" : "issuer_jwt_hash");

        if (!string.Equals(expectedBinding, actualBinding, StringComparison.Ordinal))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalidSignature, $"Token {index} is not bound to token {index - 1}.");
        }

        if (isTerminal)
        {
            if (!JoseJson.TryGetNumber(payload["iat"], out _))
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"Token {index} has no iat claim.");
            }

            if (isLast)
            {
                var failure = context.CheckAudienceAndNonce(index, payload);

                if (failure is not null)
                {
                    return failure;
                }
            }
        }

        var hasConfirmation = hop.Items.Exists(item => item["cnf"] is JsonObject);

        if (isTerminal && hasConfirmation)
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"The terminal token {index} must not carry a cnf claim.");
        }

        if (!isTerminal && !hasConfirmation)
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"The intermediate token {index} must carry a cnf claim.");
        }

        return null;
    }

    private static Ap2MandateVerificationResult VerifyKeyBinding(VerifiedHop root, VerificationContext context)
    {
        var keyBindingJwt = root.Token.KeyBindingJwt;

        if (keyBindingJwt is null)
        {
            return context.Options.ExpectedAudience is not null || context.Options.ExpectedNonce is not null
                ? Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, "The mandate has no key binding to an audience and nonce.")
                : null;
        }

        var key = root.FindConfirmationKey();

        if (key is null)
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.AgentMissingKey, "The mandate has no cnf.jwk for its key binding JWT.");
        }

        JsonObject header;
        byte[] payloadBytes;

        try
        {
            using var ecdsa = key.CreateECDsa();

            if (!JwsSigner.TryVerifyCompact(keyBindingJwt, _ => ecdsa, out header, out payloadBytes))
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalidSignature, "The key binding JWT signature does not verify.");
            }
        }
        catch (CryptographicException)
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalidSignature, "The cnf.jwk of the mandate is not a valid key.");
        }

        if (JoseJson.GetString(header, "typ") != Ap2MandateTypes.KeyBindingJwtType || !JoseJson.TryParseObject(payloadBytes, out var payload))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, "The key binding JWT is malformed.");
        }

        if (!JoseJson.TryGetNumber(payload["iat"], out _))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, "The key binding JWT has no iat claim.");
        }

        if (!string.Equals(JoseJson.GetString(payload, "sd_hash"), root.Token.ComputeSdHash(), StringComparison.Ordinal))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalidSignature, "The key binding JWT is not bound to the SD-JWT.");
        }

        return context.CheckTime(0, payload) ?? context.CheckAudienceAndNonce(0, payload);
    }

    private static Ap2MandateVerificationResult VerifyCheckout(List<VerifiedHop> hops, Ap2VerificationOptions options)
    {
        var openMandates = new List<JsonObject>();

        for (var i = 0; i < hops.Count - 1; i++)
        {
            var open = hops[i].Items[0];
            var vct = JoseJson.GetString(open, "vct");

            if (vct != Ap2MandateTypes.OpenCheckoutMandate)
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateScopeMismatch, $"Token {i} is not an open checkout mandate (vct '{vct}').");
            }

            openMandates.Add(open);
        }

        var last = hops[hops.Count - 1];
        var closed = last.Items.Find(item => JoseJson.GetString(item, "vct") == Ap2MandateTypes.CheckoutMandate);

        if (closed is null)
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateScopeMismatch, "The mandate holds no closed checkout mandate (vct 'mandate.checkout.1').");
        }

        var checkoutJwt = JoseJson.GetString(closed, "checkout_jwt");
        var checkoutHash = JoseJson.GetString(closed, "checkout_hash");

        if (string.IsNullOrEmpty(checkoutJwt) || string.IsNullOrEmpty(checkoutHash))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, "The closed checkout mandate has no disclosed checkout_jwt or checkout_hash.");
        }

        if (!string.Equals(Ap2Hashing.Hash(checkoutJwt, last.Token.SdAlg), checkoutHash, StringComparison.Ordinal))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, "The checkout_hash does not match the checkout_jwt.");
        }

        if (options.ExpectedCheckoutHash is not null && !string.Equals(options.ExpectedCheckoutHash, checkoutHash, StringComparison.Ordinal))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateScopeMismatch, "The checkout_hash is not the expected checkout.");
        }

        var checkoutParts = checkoutJwt.Split('.');

        if (checkoutParts.Length != 3 || !JoseJson.TryDecodeObjectSegment(checkoutParts[1], out var checkout))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, "The checkout_jwt is malformed.");
        }

        if (options.MerchantKeyResolver is not null
            && !JwtEs256.TryVerify(checkoutJwt, header => options.MerchantKeyResolver(JoseJson.GetString(header, "kid")), out _, out _))
        {
            return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MerchantAuthorizationInvalid, "The merchant signature of the checkout_jwt does not verify.");
        }

        foreach (var open in openMandates)
        {
            var violations = Ap2CheckoutConstraints.Evaluate(open, checkout);

            if (violations.Count > 0)
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateScopeMismatch, string.Join(" ", violations));
            }
        }

        return new Ap2MandateVerificationResult
        {
            Succeeded = true,
            CheckoutJwt = checkoutJwt,
            CheckoutHash = checkoutHash,
            Checkout = checkout,
            ClosedMandate = closed,
            ClosedMandateHash = Ap2Hashing.Hash(last.Token.IssuerJwt, Ap2Hashing.Sha256),
            OpenMandateHash = hops.Count > 1
                ? hops[0].Token.ComputeSdHash()
                : null,
            Constraints = openMandates.Count > 0
                ? openMandates[openMandates.Count - 1]
                : null,
        };
    }

    private static Ap2MandateVerificationResult ResolveRootKey(SdJwt root, Ap2VerificationOptions options, out EcJsonWebKey key)
    {
        key = null;

        if (root.IssuerHeader["x5c"] is JsonArray x5c && options.TrustedRoots is { Count: > 0 })
        {
            if (!TryResolveCertificateKey(x5c, root, options, out key))
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalidSignature, "The x5c certificate chain of the root token is not trusted.");
            }

            return null;
        }

        var rootContext = new JsonObject
        {
            ["header"] = root.IssuerHeader.DeepClone(),
            ["payload"] = root.IssuerPayload.DeepClone(),
        };

        key = options.RootKeyResolver?.Invoke(rootContext);

        return key is null
            ? Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.AgentMissingKey, "No trusted key is known for the root token of the mandate.")
            : null;
    }

    private static bool TryResolveCertificateKey(JsonArray x5c, SdJwt root, Ap2VerificationOptions options, out EcJsonWebKey key)
    {
        key = null;
        var certificates = new List<X509Certificate2>();

        try
        {
            foreach (var node in x5c)
            {
                if (!JoseJson.IsJsonString(node, out var encoded) || !TryDecodeCertificate(encoded, out var der))
                {
                    return false;
                }

                certificates.Add(X509CertificateLoader.LoadCertificate(der));
            }

            if (certificates.Count == 0)
            {
                return false;
            }

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationTime = options.Now.UtcDateTime;

            foreach (var trustedRoot in options.TrustedRoots)
            {
                chain.ChainPolicy.CustomTrustStore.Add(trustedRoot);
            }

            for (var i = 1; i < certificates.Count; i++)
            {
                chain.ChainPolicy.ExtraStore.Add(certificates[i]);
            }

            if (!chain.Build(certificates[0]))
            {
                return false;
            }

            using var publicKey = certificates[0].GetECDsaPublicKey();

            if (publicKey is null)
            {
                return false;
            }

            key = EcJsonWebKey.FromECDsa(publicKey, JoseJson.GetString(root.IssuerHeader, "kid"), false);

            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            foreach (var certificate in certificates)
            {
                certificate.Dispose();
            }
        }
    }

    private static bool TryDecodeCertificate(string encoded, out byte[] der)
    {
        try
        {
            der = Convert.FromBase64String(encoded);

            return true;
        }
        catch (FormatException)
        {
            return JoseJson.TryDecodeBase64Url(encoded, out der);
        }
    }

    private static bool VerifyIssuerSignature(SdJwt token, EcJsonWebKey key)
    {
        try
        {
            using var ecdsa = key.CreateECDsa();

            return JwsSigner.TryVerifyCompact(token.IssuerJwt, _ => ecdsa, out _, out _);
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static string CanonicalSegment(string segment, bool isLast)
    {
        if (isLast || segment.EndsWith('~'))
        {
            return segment;
        }

        var lastSeparator = segment.LastIndexOf('~');

        if (lastSeparator < 0)
        {
            return segment + "~";
        }

        return segment.Substring(lastSeparator + 1).Split('.').Length == 3
            ? segment
            : segment + "~";
    }

    private sealed class VerifiedHop
    {
        public VerifiedHop(SdJwt token, JsonObject payload)
        {
            Token = token;
            Payload = payload;
            Items = [];

            if (payload["delegate_payload"] is JsonArray delegatePayload)
            {
                foreach (var node in delegatePayload)
                {
                    if (node is JsonObject item)
                    {
                        Items.Add(item);
                    }
                    else if (JoseJson.IsJsonString(node, out var encoded)
                        && SdJwtDisclosure.TryParse(encoded, out var disclosure)
                        && disclosure.Value is JsonObject disclosed)
                    {
                        Items.Add(disclosed);
                    }
                }
            }

            if (Items.Count == 0)
            {
                Items.Add(payload);
            }
        }

        public SdJwt Token { get; }

        public JsonObject Payload { get; }

        public List<JsonObject> Items { get; }

        public EcJsonWebKey FindConfirmationKey()
        {
            foreach (var item in Items)
            {
                if (item["cnf"] is JsonObject cnf && cnf["jwk"] is JsonObject jwk)
                {
                    return EcJsonWebKey.FromJson(jwk);
                }
            }

            return Payload["cnf"] is JsonObject payloadCnf && payloadCnf["jwk"] is JsonObject payloadJwk
                ? EcJsonWebKey.FromJson(payloadJwk)
                : null;
        }
    }

    private sealed class VerificationContext
    {
        private readonly long _now;
        private readonly long _skew;

        public VerificationContext(Ap2VerificationOptions options)
        {
            Options = options;
            _now = options.Now.ToUnixTimeSeconds();
            _skew = (long)options.ClockSkew.TotalSeconds;
        }

        public Ap2VerificationOptions Options { get; }

        public Ap2MandateVerificationResult CheckTimes(int index, VerifiedHop hop)
        {
            var failure = CheckTime(index, hop.Payload);

            foreach (var item in hop.Items)
            {
                if (failure is not null)
                {
                    break;
                }

                if (!ReferenceEquals(item, hop.Payload))
                {
                    failure = CheckTime(index, item);
                }
            }

            return failure;
        }

        public Ap2MandateVerificationResult CheckTime(int index, JsonObject claims)
        {
            if (claims.TryGetPropertyValue("exp", out var expNode) && expNode is not null)
            {
                if (!JoseJson.TryGetNumber(expNode, out var exp))
                {
                    return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"Token {index} has an exp claim that is not a number.");
                }

                if (_now > exp + _skew)
                {
                    return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateExpired, $"Token {index} expired.");
                }
            }

            if (claims.TryGetPropertyValue("iat", out var iatNode) && iatNode is not null)
            {
                if (!JoseJson.TryGetNumber(iatNode, out var iat))
                {
                    return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"Token {index} has an iat claim that is not a number.");
                }

                if (iat > _now + _skew)
                {
                    return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateInvalid, $"Token {index} was issued in the future.");
                }
            }

            return null;
        }

        public Ap2MandateVerificationResult CheckAudienceAndNonce(int index, JsonObject claims)
        {
            if (Options.ExpectedAudience is not null && !AudienceMatches(claims["aud"], Options.ExpectedAudience))
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateScopeMismatch, $"Token {index} is for another audience.");
            }

            if (Options.ExpectedNonce is not null && JoseJson.GetString(claims, "nonce") != Options.ExpectedNonce)
            {
                return Ap2MandateVerificationResult.Fail(Ap2ErrorCodes.MandateScopeMismatch, $"Token {index} carries another nonce.");
            }

            return null;
        }

        private static bool AudienceMatches(JsonNode audience, string expected)
        {
            if (JoseJson.IsJsonString(audience, out var single))
            {
                return single == expected;
            }

            if (audience is JsonArray audiences)
            {
                foreach (var node in audiences)
                {
                    if (JoseJson.IsJsonString(node, out var value) && value == expected)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
