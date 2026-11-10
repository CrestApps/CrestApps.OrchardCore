using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// An ACP platform may sign its requests: an ES256 signature (base64url) of the RFC 8785 canonical JSON of the body in
/// <c>Signature</c>, and the time it signed in <c>Timestamp</c>, which must be within the platform's clock skew. A
/// platform that requires signed requests must sign every one; another platform's signed request is checked when it
/// has a public key.
/// </summary>
public sealed class AcpRequestSignatureTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private const string Timestamp = "2026-11-10T12:00:00Z";

    private const string Body = "{\"line_items\":[{\"id\":\"sku-1\",\"quantity\":2}],\"currency\":\"usd\"}";

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public void Dispose()
        => _key.Dispose();

    [Fact]
    public void Verify_GoodSignature_IsValid()
    {
        // Arrange
        var signature = Sign(Body);

        // Act
        var result = AcpRequestSignature.Verify(Input(Body), signature, Timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.Valid, result);
    }

    [Fact]
    public void Verify_TamperedBody_IsInvalid()
    {
        // Arrange
        var signature = Sign(Body);

        // Act
        var result = AcpRequestSignature.Verify(Input(Body.Replace("\"quantity\":2", "\"quantity\":20")), signature, Timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.InvalidSignature, result);
    }

    [Theory]
    [InlineData("2026-11-10T11:54:59Z")]
    [InlineData("2026-11-10T12:05:01Z")]
    [InlineData("2026-11-09T12:00:00+00:00")]
    public void Verify_TimestampOutsideTheSkew_IsStale(string timestamp)
    {
        // Arrange
        var signature = Sign(Body);

        // Act
        var result = AcpRequestSignature.Verify(Input(Body), signature, timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.StaleTimestamp, result);
    }

    [Theory]
    [InlineData("2026-11-10T11:55:00Z")]
    [InlineData("2026-11-10T07:04:00-05:00")]
    [InlineData("2026-11-10T12:04:59.123456Z")]
    public void Verify_TimestampInsideTheSkew_IsValid(string timestamp)
    {
        // Arrange
        var signature = Sign(Body);

        // Act
        var result = AcpRequestSignature.Verify(Input(Body), signature, timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.Valid, result);
    }

    [Theory]
    [InlineData("1731240000")]
    [InlineData("2026-11-10T12:00:00")]
    [InlineData("11/10/2026 12:00:00")]
    public void Verify_TimestampThatIsNotRfc3339_IsInvalid(string timestamp)
    {
        // Act
        var result = AcpRequestSignature.Verify(Input(Body), Sign(Body), timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.InvalidTimestamp, result);
    }

    [Fact]
    public void Verify_WithAnotherKey_IsInvalid()
    {
        // Arrange
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signature = AcpRequestSignature.Sign(Input(Body), other);

        // Act
        var result = AcpRequestSignature.Verify(Input(Body), signature, Timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.InvalidSignature, result);
    }

    [Theory]
    [InlineData(null, Timestamp)]
    [InlineData("c2lnbmF0dXJl", null)]
    [InlineData("", "")]
    public void Verify_WithAMissingHeader_IsMissing(string signature, string timestamp)
    {
        // Act
        var result = AcpRequestSignature.Verify(Input(Body), signature, timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.MissingHeaders, result);
    }

    [Fact]
    public void Verify_IgnoresKeyOrderAndWhitespace()
    {
        // Arrange: the agent signs one serialization and sends another of the same JSON.
        var signature = Sign(Body);
        const string reordered = "{ \"currency\" : \"usd\",\n  \"line_items\" : [ { \"quantity\": 2.0, \"id\": \"sku-1\" } ] }";

        // Act
        var result = AcpRequestSignature.Verify(Input(reordered), signature, Timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.Valid, result);
    }

    [Fact]
    public void Verify_ArrayOrderStillMatters()
    {
        // Arrange
        const string body = "{\"line_items\":[{\"id\":\"a\"},{\"id\":\"b\"}]}";
        var signature = Sign(body);

        // Act
        var result = AcpRequestSignature.Verify(Input("{\"line_items\":[{\"id\":\"b\"},{\"id\":\"a\"}]}"), signature, Timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.InvalidSignature, result);
    }

    [Fact]
    public void Verify_DerSignature_IsAccepted()
    {
        // Arrange
        var der = _key.SignData(Input(Body), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        // Act
        var result = AcpRequestSignature.Verify(Input(Body), Base64UrlEncode(der), Timestamp, PublicJwk(), AcpRequestSignature.DefaultClockSkew, _now);

        // Assert
        Assert.Equal(AcpSignatureVerification.Valid, result);
    }

    [Fact]
    public void SigningInput_ForAnEmptyBody_IsEmpty()
    {
        // Act
        var parsed = AcpRequestSignature.TryGetSigningInput([], out var input);

        // Assert
        Assert.True(parsed);
        Assert.Empty(input);
        Assert.Empty(AcpRequestSignature.GetSigningInput(null));
    }

    [Fact]
    public void SigningInput_ForABodyThatIsNotJson_CannotBeMade()
    {
        // Act
        var parsed = AcpRequestSignature.TryGetSigningInput(Encoding.UTF8.GetBytes("not json"), out _);

        // Assert
        Assert.False(parsed);
    }

    [Fact]
    public void TryReadPublicKey_ReadsAJwkAndAPem()
    {
        // Arrange
        var jwk = PublicJwk().ToJson().ToJsonString();
        var pem = _key.ExportSubjectPublicKeyInfoPem();

        // Act
        var fromJwk = AcpRequestSignature.TryReadPublicKey(jwk, out var jwkKey);
        var fromPem = AcpRequestSignature.TryReadPublicKey(pem, out var pemKey);

        // Assert
        Assert.True(fromJwk);
        Assert.True(fromPem);
        Assert.Equal(jwkKey.X, pemKey.X);
        Assert.Equal(jwkKey.Y, pemKey.Y);
    }

    [Fact]
    public void TryReadPublicKey_RefusesPrivateKeysAndOtherCurves()
    {
        // Arrange
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var privateJwk = EcJsonWebKey.FromECDsa(_key, "k1", includePrivateKey: true).ToJson(includePrivateKey: true).ToJsonString();

        // Act and Assert
        Assert.False(AcpRequestSignature.TryReadPublicKey(privateJwk, out _));
        Assert.False(AcpRequestSignature.TryReadPublicKey(_key.ExportPkcs8PrivateKeyPem(), out _));
        Assert.False(AcpRequestSignature.TryReadPublicKey(p384.ExportSubjectPublicKeyInfoPem(), out _));
        Assert.False(AcpRequestSignature.TryReadPublicKey("not a key", out _));
        Assert.False(AcpRequestSignature.TryReadPublicKey("{\"kty\":\"RSA\"}", out _));
    }

    [Fact]
    public void Check_RequiredAndUnsigned_IsRefused()
    {
        // Arrange
        var platform = Platform(required: true);

        // Act
        var error = AcpSignatureCheck.Check(platform, Input(Body), signature: null, timestamp: null, _now);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Check_RequiredAndSigned_IsAccepted()
    {
        // Arrange
        var platform = Platform(required: true);

        // Act
        var error = AcpSignatureCheck.Check(platform, Input(Body), Sign(Body), Timestamp, _now);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Check_RequiredWithAPemKey_IsAccepted()
    {
        // Arrange
        var platform = Platform(required: true);
        platform.AcpSignaturePublicKey = _key.ExportSubjectPublicKeyInfoPem();

        // Act
        var error = AcpSignatureCheck.Check(platform, Input(Body), Sign(Body), Timestamp, _now);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Check_NotRequiredAndUnsigned_IsAccepted()
    {
        // Arrange
        var platform = Platform(required: false);

        // Act
        var error = AcpSignatureCheck.Check(platform, Input(Body), signature: null, timestamp: null, _now);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Check_NotRequiredButSignedWrongly_IsRefused()
    {
        // Arrange
        var platform = Platform(required: false);

        // Act
        var error = AcpSignatureCheck.Check(platform, Input("{\"other\":true}"), Sign(Body), Timestamp, _now);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void Check_NotRequiredWithoutAKey_IgnoresTheSignature()
    {
        // Arrange
        var platform = new AgentPlatform();

        // Act
        var error = AcpSignatureCheck.Check(platform, Input(Body), "anything", Timestamp, _now);

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void Check_UsesThePlatformsClockSkew()
    {
        // Arrange
        var platform = Platform(required: true);
        platform.AcpSignatureClockSkewSeconds = 30;

        // Act
        var stale = AcpSignatureCheck.Check(platform, Input(Body), Sign(Body), "2026-11-10T11:59:00Z", _now);
        var fresh = AcpSignatureCheck.Check(platform, Input(Body), Sign(Body), "2026-11-10T11:59:45Z", _now);

        // Assert
        Assert.NotNull(stale);
        Assert.Null(fresh);
    }

    [Fact]
    public void Check_ABodyThatIsNotJson_IsRefusedWhenRequired()
    {
        // Arrange
        var platform = Platform(required: true);

        // Act
        var error = AcpSignatureCheck.Check(platform, input: null, Sign(Body), Timestamp, _now);

        // Assert
        Assert.NotNull(error);
    }

    private AgentPlatform Platform(bool required)
        => new()
        {
            AcpRequireSignedRequests = required,
            AcpSignatureAlgorithm = AcpRequestSignature.ES256,
            AcpSignaturePublicKey = PublicJwk().ToJson().ToJsonString(),
        };

    private EcJsonWebKey PublicJwk()
        => EcJsonWebKey.FromECDsa(_key, "agent-1", includePrivateKey: false);

    private string Sign(string body)
        => AcpRequestSignature.Sign(Input(body), _key);

    private static byte[] Input(string body)
        => AcpRequestSignature.GetSigningInput(JsonNode.Parse(body));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
