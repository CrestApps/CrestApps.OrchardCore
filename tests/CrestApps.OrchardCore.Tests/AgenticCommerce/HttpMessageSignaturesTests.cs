using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// RFC 9421 HTTP message signatures with ECDSA keys, checked against the RFC's Appendix B.2.4 example, plus the
/// UCP default components and the RFC 9530 Content-Digest header.
/// </summary>
public sealed class HttpMessageSignaturesTests
{
    private const string B24SignatureInput =
        "sig-b24=(\"@status\" \"content-type\" \"content-digest\" \"content-length\");created=1618884473;keyid=\"test-key-ecc-p256\"";

    private const string B24Signature =
        "sig-b24=:wNmSUAhwb5LxtOtOpNa6W5xj067m5hFrj0XQ4fvpaCLx0NKocgPquLgyahnzDnDAUy5eCdlYUEkLIj+32oiasw==:";

    private const string B24ContentDigest =
        "sha-512=:mEWXIS7MaLRuGgxOBdODa3xqM1XdEvxoYhvlCFJ41QJgJc4GTsPp29l5oGX69wWdXymyU0rjJuahq4l5aGgfLQ==:";

    private static EcJsonWebKey TestKeyEccP256 => EcJsonWebKey.FromJson(new JsonObject
    {
        ["kty"] = "EC",
        ["crv"] = "P-256",
        ["kid"] = "test-key-ecc-p256",
        ["d"] = "UpuF81l-kOxbjf7T4mNSv0r5tN67Gim7rnf6EFpcYDs",
        ["x"] = "qIVYZVLCrPZHGHjP17CTW0_-D9Lfw0EkjqF7xB4FivA",
        ["y"] = "Mc4nN9LTDOBhfoUeg8Ye9WedFRhnZXZJA12Qp0zZ6F0",
    });

    [Fact]
    public void BuildSignatureBase_Rfc9421AppendixB24_MatchesTheExample()
    {
        // Arrange
        var message = CreateB24Response();

        // Act
        var signatureBase = HttpMessageSignatures.BuildSignatureBase(
            message,
            ["@status", "content-type", "content-digest", "content-length"],
            "(\"@status\" \"content-type\" \"content-digest\" \"content-length\");created=1618884473;keyid=\"test-key-ecc-p256\"");

        // Assert
        Assert.Equal(
            string.Join(
                "\n",
                "\"@status\": 200",
                "\"content-type\": application/json",
                "\"content-digest\": " + B24ContentDigest,
                "\"content-length\": 23",
                "\"@signature-params\": (\"@status\" \"content-type\" \"content-digest\" \"content-length\");created=1618884473;keyid=\"test-key-ecc-p256\""),
            signatureBase);
    }

    [Fact]
    public void Verify_Rfc9421AppendixB24Signature_Succeeds()
    {
        // Arrange
        var message = CreateB24Response();
        var publicKey = EcJsonWebKey.FromJson(TestKeyEccP256.ToJson());

        // Act
        var result = HttpMessageSignatures.Verify(message, B24SignatureInput, B24Signature, keyId => keyId == "test-key-ecc-p256" ? publicKey : null);

        // Assert
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("sig-b24", result.Label);
        Assert.Equal("test-key-ecc-p256", result.KeyId);
        Assert.Equal(["@status", "content-type", "content-digest", "content-length"], result.CoveredComponents);
        Assert.True(ContentDigest.Verify(B24ContentDigest, Encoding.UTF8.GetBytes("{\"message\": \"good dog\"}")));
    }

    [Fact]
    public void SignThenVerify_UcpRequest_SucceedsWithTheDefaultComponents()
    {
        // Arrange
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var jwk = EcJsonWebKey.FromECDsa(ecdsa, "platform-2026", includePrivateKey: false);
        var message = CreateRequest();
        var components = HttpMessageSignatures.DefaultRequestComponents(hasBody: true, hasQuery: true, hasUcpAgent: true, hasIdempotencyKey: true);

        // Act
        var signature = HttpMessageSignatures.Sign(message, components, "platform-2026", ecdsa);
        var result = HttpMessageSignatures.Verify(message, signature.SignatureInput, signature.Signature, keyId => keyId == "platform-2026" ? jwk : null);

        // Assert
        Assert.Equal(
            "sig1=(\"@method\" \"@authority\" \"@path\" \"@query\" \"ucp-agent\" \"idempotency-key\" \"content-digest\" \"content-type\");keyid=\"platform-2026\"",
            signature.SignatureInput);
        Assert.Matches("^sig1=:[A-Za-z0-9+/]+=*:$", signature.Signature);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal("sig1", result.Label);
    }

    [Fact]
    public void Sign_WithCreated_AddsTheCreatedParameterAfterTheKeyId()
    {
        // Arrange
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Act
        var signature = HttpMessageSignatures.Sign(CreateB24Response(), HttpMessageSignatures.DefaultResponseComponents(hasBody: true), "biz-1", ecdsa, "sig2", 1_800_000_000);

        // Assert
        Assert.Equal("sig2=(\"@status\" \"content-digest\" \"content-type\");keyid=\"biz-1\";created=1800000000", signature.SignatureInput);
    }

    [Fact]
    public void Verify_FailureCases_ReturnTheUcpErrorCodes()
    {
        // Arrange
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var jwk = EcJsonWebKey.FromECDsa(ecdsa, "platform-2026", includePrivateKey: false);
        var p384Jwk = EcJsonWebKey.FromECDsa(p384, "p384", includePrivateKey: false);
        var message = CreateRequest();
        var components = HttpMessageSignatures.DefaultRequestComponents(true, true, true, true);
        var signature = HttpMessageSignatures.Sign(message, components, "platform-2026", ecdsa);

        var tampered = CreateRequest();
        tampered.Headers["Idempotency-Key"] = "another-key";

        var missingHeader = CreateRequest();
        missingHeader.Headers.Remove("UCP-Agent");

        // Act
        var missing = HttpMessageSignatures.Verify(message, null, signature.Signature, _ => jwk);
        var garbage = HttpMessageSignatures.Verify(message, "sig1=(", signature.Signature, _ => jwk);
        var otherLabel = HttpMessageSignatures.Verify(message, signature.SignatureInput.Replace("sig1=", "sig9="), signature.Signature, _ => jwk);
        var unknownKey = HttpMessageSignatures.Verify(message, signature.SignatureInput, signature.Signature, _ => null);
        var invalid = HttpMessageSignatures.Verify(tampered, signature.SignatureInput, signature.Signature, _ => jwk);
        var componentMissing = HttpMessageSignatures.Verify(missingHeader, signature.SignatureInput, signature.Signature, _ => jwk);
        var ed25519 = HttpMessageSignatures.Verify(message, signature.SignatureInput + ";alg=\"ed25519\"", signature.Signature, _ => jwk);
        var wrongCurve = HttpMessageSignatures.Verify(message, signature.SignatureInput, signature.Signature, _ => p384Jwk);

        // Assert
        Assert.Equal(HttpSignatureErrorCodes.SignatureMissing, missing.ErrorCode);
        Assert.Equal(HttpSignatureErrorCodes.SignatureMissing, garbage.ErrorCode);
        Assert.Equal(HttpSignatureErrorCodes.SignatureMissing, otherLabel.ErrorCode);
        Assert.Equal(HttpSignatureErrorCodes.KeyNotFound, unknownKey.ErrorCode);
        Assert.Equal(HttpSignatureErrorCodes.SignatureInvalid, invalid.ErrorCode);
        Assert.Equal(HttpSignatureErrorCodes.SignatureInvalid, componentMissing.ErrorCode);
        Assert.Equal(HttpSignatureErrorCodes.AlgorithmUnsupported, ed25519.ErrorCode);
        Assert.Equal(HttpSignatureErrorCodes.SignatureInvalid, wrongCurve.ErrorCode);
        Assert.False(invalid.Succeeded);
    }

    [Fact]
    public void BuildSignatureBase_DerivedComponents_FollowRfc9421()
    {
        // Arrange
        var message = new HttpSignatureMessage
        {
            Method = "POST",
            Authority = "Example.COM",
            Path = string.Empty,
        };

        // Act
        var signatureBase = HttpMessageSignatures.BuildSignatureBase(message, ["@method", "@authority", "@path", "@query", "@target-uri"], "()");

        // Assert
        Assert.Equal(
            string.Join(
                "\n",
                "\"@method\": POST",
                "\"@authority\": example.com",
                "\"@path\": /",
                "\"@query\": ?",
                "\"@target-uri\": https://example.com/",
                "\"@signature-params\": ()"),
            signatureBase);
        Assert.Throws<InvalidOperationException>(() => HttpMessageSignatures.BuildSignatureBase(message, ["@status"], "()"));
    }

    [Fact]
    public void ContentDigest_ComputeAndVerify_FollowRfc9530()
    {
        // Arrange
        var body = Encoding.UTF8.GetBytes("{\"hello\": \"world\"}");

        // Act
        var digest = ContentDigest.ComputeSha256(body);

        // Assert
        Assert.Equal("sha-256=:X48E9qOokqqrvdts8nOJRJN3OWDUoyWxBf7kbu9DBPE=:", digest);
        Assert.True(ContentDigest.Verify(digest, body));
        Assert.True(ContentDigest.Verify("sha-512=:WZDPaVn/7XgHaAy8pmojAkGWoRx2UFChF41A2svX+TaPm+AbwAgBWnrIiYllu7BNNyealdVLvRwEmTHWXvJwew==:, md5=:AAAA:", body));
        Assert.False(ContentDigest.Verify(digest, Encoding.UTF8.GetBytes("{\"hello\": \"world!\"}")));
        Assert.False(ContentDigest.Verify("md5=:AAAA:", body));
        Assert.False(ContentDigest.Verify(null, body));
    }

    private static HttpSignatureMessage CreateB24Response()
    {
        return new HttpSignatureMessage
        {
            Status = 200,
            Headers =
            {
                ["Date"] = "Tue, 20 Apr 2021 02:07:56 GMT",
                ["Content-Type"] = "application/json",
                ["Content-Digest"] = B24ContentDigest,
                ["Content-Length"] = "23",
            },
        };
    }

    private static HttpSignatureMessage CreateRequest()
    {
        var body = Encoding.UTF8.GetBytes("{\"line_items\":[]}");

        return new HttpSignatureMessage
        {
            Method = "POST",
            Authority = "shop.example",
            Path = "/ucp/checkout-sessions",
            Query = "?lang=en",
            Headers =
            {
                ["UCP-Agent"] = "profile=\"https://agent.example/.well-known/ucp\"",
                ["Idempotency-Key"] = "8e0d3c1a",
                ["Content-Type"] = "application/json",
                ["Content-Digest"] = ContentDigest.ComputeSha256(body),
            },
        };
    }
}
