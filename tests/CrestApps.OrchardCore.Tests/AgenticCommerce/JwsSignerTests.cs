using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// ES256/ES384/ES512 JWS in the compact and detached forms, with the RFC 7515 Appendix A.3 example.
/// </summary>
public sealed class JwsSignerTests
{
    private const string Rfc7515Jws =
        "eyJhbGciOiJFUzI1NiJ9" +
        ".eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ" +
        ".DtEhU3ljbEg8L38VWAfUAqOyKAM6-Xx-F4GawxaepmXFCgfTjDxw5djxLa8ISlSApmWQxfKTUJqPP3-Kg6NU1Q";

    private static EcJsonWebKey Rfc7515Key => EcJsonWebKey.FromJson(new JsonObject
    {
        ["kty"] = "EC",
        ["crv"] = "P-256",
        ["x"] = "f83OJ3D2xF1Bg8vub9tLe1gHMzV76e8Tus9uPHvRVEU",
        ["y"] = "x_FEzRu9m36HLN_tue659LNpXW6pCyStikYjKIWI5a0",
        ["d"] = "jpsQnnGQmL-YBIffH1136cspYG6-0iY7X1fCE9-E9LI",
    });

    [Fact]
    public void TryVerifyCompact_Rfc7515AppendixA3_Verifies()
    {
        // Arrange
        using var key = Rfc7515Key.CreateECDsa();

        // Act
        var verified = JwsSigner.TryVerifyCompact(Rfc7515Jws, _ => key, out var header, out var payload);

        // Assert
        Assert.True(verified);
        Assert.Equal("ES256", header["alg"].GetValue<string>());
        Assert.StartsWith("{\"iss\":\"joe\"", Encoding.UTF8.GetString(payload));
    }

    [Theory]
    [InlineData(256, "ES256")]
    [InlineData(384, "ES384")]
    [InlineData(521, "ES512")]
    public void SignCompact_EachAlgorithm_RoundTrips(int keySize, string algorithm)
    {
        // Arrange
        using var key = CreateKey(keySize);
        var header = new JsonObject { ["alg"] = algorithm, ["typ"] = "kb+sd-jwt", ["kid"] = "k" };
        var payload = Encoding.UTF8.GetBytes("{\"a\":1}");

        // Act
        var jws = JwsSigner.SignCompact(header, payload, key);
        var verified = JwsSigner.TryVerifyCompact(jws, _ => key, out var verifiedHeader, out var verifiedPayload);

        // Assert
        Assert.True(verified);
        Assert.Equal("kb+sd-jwt", verifiedHeader["typ"].GetValue<string>());
        Assert.Equal(payload, verifiedPayload);
        Assert.Equal(JwsAlgorithmsSignatureSize(keySize), Convert.FromBase64String(ToBase64(jws.Split('.')[2])).Length);
    }

    [Fact]
    public void SignDetached_VerifiesOnlyAgainstTheSamePayload()
    {
        // Arrange
        using var key = CreateKey(256);
        var header = new JsonObject { ["alg"] = "ES256", ["kid"] = "merchant-1" };
        var payload = Encoding.UTF8.GetBytes("{\"id\":\"chk_1\"}");

        // Act
        var detached = JwsSigner.SignDetached(header, payload, key);
        var valid = JwsSigner.VerifyDetached(detached, payload, _ => key, out var verifiedHeader);
        var tampered = JwsSigner.VerifyDetached(detached, Encoding.UTF8.GetBytes("{\"id\":\"chk_2\"}"), _ => key, out _);

        // Assert
        Assert.Matches("^[A-Za-z0-9_-]+\\.\\.[A-Za-z0-9_-]+$", detached);
        Assert.True(valid);
        Assert.Equal("merchant-1", verifiedHeader["kid"].GetValue<string>());
        Assert.False(tampered);
    }

    [Fact]
    public void TryVerifyCompact_Rejections_ReturnFalse()
    {
        // Arrange
        using var key = CreateKey(256);
        using var otherKey = CreateKey(256);
        using var p384 = CreateKey(384);
        var payload = Encoding.UTF8.GetBytes("{}");
        var jws = JwsSigner.SignCompact(new JsonObject { ["alg"] = "ES256" }, payload, key);
        var parts = jws.Split('.');
        var noneHeader = ToBase64Url(Encoding.UTF8.GetBytes("{\"alg\":\"none\"}"));
        var hsHeader = ToBase64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\"}"));

        // Act and Assert
        Assert.False(JwsSigner.TryVerifyCompact(jws, _ => otherKey, out _, out _));
        Assert.False(JwsSigner.TryVerifyCompact(jws, _ => null, out _, out _));
        Assert.False(JwsSigner.TryVerifyCompact(jws, _ => p384, out _, out _));
        Assert.False(JwsSigner.TryVerifyCompact(noneHeader + "." + parts[1] + ".", _ => key, out _, out _));
        Assert.False(JwsSigner.TryVerifyCompact(hsHeader + "." + parts[1] + "." + parts[2], _ => key, out _, out _));
        Assert.False(JwsSigner.TryVerifyCompact(parts[0] + "." + ToBase64Url(Encoding.UTF8.GetBytes("{\"a\":2}")) + "." + parts[2], _ => key, out _, out _));
        Assert.False(JwsSigner.TryVerifyCompact("not-a-jws", _ => key, out _, out _));
    }

    [Fact]
    public void SignCompact_AlgorithmThatDoesNotMatchTheCurve_Throws()
    {
        // Arrange
        using var key = CreateKey(256);

        // Act and Assert
        Assert.Throws<ArgumentException>(() => JwsSigner.SignCompact(new JsonObject { ["alg"] = "ES384" }, [1], key));
        Assert.Throws<ArgumentException>(() => JwsSigner.SignCompact(new JsonObject { ["alg"] = "none" }, [1], key));
    }

    [Fact]
    public void JwtEs256_CreateThenVerify_ReadsTheClaims()
    {
        // Arrange
        using var ecdsa = CreateKey(256);
        var jwk = EcJsonWebKey.FromECDsa(ecdsa, "merchant-key-1", includePrivateKey: true);

        // Act
        var jwt = JwtEs256.Create(new JsonObject { ["iss"] = "https://store.example", ["iat"] = 1_800_000_000L }, jwk);
        var verified = JwtEs256.TryVerify(jwt, header => header["kid"]?.GetValue<string>() == "merchant-key-1" ? jwk : null, out var header, out var claims);

        // Assert
        Assert.True(verified);
        Assert.Equal("{\"alg\":\"ES256\",\"typ\":\"JWT\",\"kid\":\"merchant-key-1\"}", Encoding.UTF8.GetString(Convert.FromBase64String(ToBase64(jwt.Split('.')[0]))));
        Assert.Equal("JWT", header["typ"].GetValue<string>());
        Assert.Equal("https://store.example", claims["iss"].GetValue<string>());
    }

    private static ECDsa CreateKey(int keySize)
    {
        return ECDsa.Create(keySize switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            _ => ECCurve.NamedCurves.nistP521,
        });
    }

    private static int JwsAlgorithmsSignatureSize(int keySize)
    {
        return 2 * ((keySize + 7) / 8);
    }

    private static string ToBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string ToBase64(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');

        return base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
    }
}
