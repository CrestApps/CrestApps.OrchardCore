using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// EC JSON Web Keys read, write, convert to ECDSA keys and compute RFC 7638 thumbprints.
/// </summary>
public sealed class EcJsonWebKeyTests
{
    [Theory]
    [InlineData(256, "P-256", "ES256")]
    [InlineData(384, "P-384", "ES384")]
    [InlineData(521, "P-521", "ES512")]
    public void FromECDsa_EachCurve_RoundTripsThroughJson(int keySize, string curve, string algorithm)
    {
        // Arrange
        byte[] data = [1, 2, 3];
        using var ecdsa = ECDsa.Create(keySize switch
        {
            256 => ECCurve.NamedCurves.nistP256,
            384 => ECCurve.NamedCurves.nistP384,
            _ => ECCurve.NamedCurves.nistP521,
        });

        // Act
        var jwk = EcJsonWebKey.FromECDsa(ecdsa, "k1", includePrivateKey: true);
        var publicJson = jwk.ToJson();
        var privateJson = jwk.ToJson(includePrivateKey: true);
        var reloaded = EcJsonWebKey.FromJson(privateJson);
        using var recreated = reloaded.CreateECDsa();
        var signature = recreated.SignData(data, HashAlgorithmName.SHA256);

        // Assert
        Assert.Equal(curve, jwk.Crv);
        Assert.Equal(algorithm, jwk.GetAlgorithm());
        Assert.False(publicJson.ContainsKey("d"));
        Assert.True(privateJson.ContainsKey("d"));
        Assert.Equal("k1", reloaded.Kid);
        Assert.True(ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256));
    }

    [Fact]
    public void FromJson_InvalidKeys_ReturnsNull()
    {
        // Arrange
        var rsa = new JsonObject { ["kty"] = "RSA", ["n"] = "AQAB", ["e"] = "AQAB" };
        var shortCoordinate = new JsonObject { ["kty"] = "EC", ["crv"] = "P-256", ["x"] = "AQAB", ["y"] = "AQAB" };
        var unknownCurve = new JsonObject { ["kty"] = "EC", ["crv"] = "secp256k1", ["x"] = "AQAB", ["y"] = "AQAB" };

        // Act and Assert
        Assert.Null(EcJsonWebKey.FromJson(rsa));
        Assert.Null(EcJsonWebKey.FromJson(shortCoordinate));
        Assert.Null(EcJsonWebKey.FromJson(unknownCurve));
        Assert.Null(EcJsonWebKey.FromJson(null));
    }

    [Fact]
    public void ComputeThumbprint_PublicAndPrivateForms_AreEqualAndStable()
    {
        // Arrange
        var jwk = EcJsonWebKey.FromJson(new JsonObject
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["kid"] = "test-key-ecc-p256",
            ["d"] = "UpuF81l-kOxbjf7T4mNSv0r5tN67Gim7rnf6EFpcYDs",
            ["x"] = "qIVYZVLCrPZHGHjP17CTW0_-D9Lfw0EkjqF7xB4FivA",
            ["y"] = "Mc4nN9LTDOBhfoUeg8Ye9WedFRhnZXZJA12Qp0zZ6F0",
        });
        var publicOnly = EcJsonWebKey.FromJson(jwk.ToJson());

        // Act
        var thumbprint = jwk.ComputeThumbprint();
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(
            "{\"crv\":\"P-256\",\"kty\":\"EC\",\"x\":\"qIVYZVLCrPZHGHjP17CTW0_-D9Lfw0EkjqF7xB4FivA\",\"y\":\"Mc4nN9LTDOBhfoUeg8Ye9WedFRhnZXZJA12Qp0zZ6F0\"}")))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Assert
        Assert.Equal(expected, thumbprint);
        Assert.Equal(thumbprint, publicOnly.ComputeThumbprint());
    }

    [Fact]
    public void JsonWebKeySetParse_ArrayOrJwks_SkipsNonEcKeys()
    {
        // Arrange
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var ec = EcJsonWebKey.FromECDsa(ecdsa, "ec-1", includePrivateKey: false).ToJson();
        var okp = new JsonObject { ["kty"] = "OKP", ["crv"] = "Ed25519", ["kid"] = "ed-1", ["x"] = "JrQLj5P_89iXES9-vFgrIy29clF9CC_oPPsw3c5D0bs" };
        var array = new JsonArray(ec.DeepClone(), okp.DeepClone());
        var jwks = new JsonObject { ["keys"] = new JsonArray(ec.DeepClone(), okp.DeepClone()) };

        // Act
        var fromArray = JsonWebKeySet.Parse(array);
        var fromJwks = JsonWebKeySet.Parse(jwks);
        var raw = JsonWebKeySet.ParseRaw(jwks);

        // Assert
        Assert.Equal("ec-1", Assert.Single(fromArray).Kid);
        Assert.Equal("ec-1", Assert.Single(fromJwks).Kid);
        Assert.Equal(2, raw.Count);
        Assert.Empty(JsonWebKeySet.Parse(new JsonObject()));
    }
}
