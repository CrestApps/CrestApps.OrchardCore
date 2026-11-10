using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SD-JWTs parse and resolve their disclosures the way RFC 9901 describes.
/// </summary>
public sealed class SdJwtTests
{
    [Fact]
    public void TryParse_TokenWithDisclosures_ReadsEveryPart()
    {
        // Arrange
        var key = Ap2TestMandates.NewKey("issuer-1");
        var token = Ap2TestMandates.Issue(
            new JsonObject { ["alg"] = "ES256", ["kid"] = "issuer-1" },
            new JsonObject { ["iat"] = 1 },
            new JsonObject { ["vct"] = "mandate.checkout.1", ["checkout_jwt"] = "a.b.c" },
            ["checkout_jwt"],
            key);

        // Act
        var parsed = SdJwt.TryParse(token, out var sdJwt);

        // Assert
        Assert.True(parsed);
        Assert.Equal(2, sdJwt.Disclosures.Count);
        Assert.Null(sdJwt.KeyBindingJwt);
        Assert.Equal("issuer-1", sdJwt.IssuerHeader["kid"].GetValue<string>());
        Assert.Equal("sha-256", sdJwt.SdAlg);
        Assert.Null(sdJwt.Disclosures[0].ClaimName);
        Assert.Equal("checkout_jwt", sdJwt.Disclosures[1].ClaimName);
        Assert.Equal("a.b.c", sdJwt.Disclosures[1].Value.GetValue<string>());
        Assert.Equal(token, sdJwt.SdJwtWithoutKeyBinding);
    }

    [Fact]
    public void ResolveClaims_NestedDisclosures_ReplacesDigestsAndDropsDecoys()
    {
        // Arrange
        var key = Ap2TestMandates.NewKey("issuer-1");
        var token = Ap2TestMandates.Issue(
            new JsonObject { ["alg"] = "ES256" },
            new JsonObject(),
            new JsonObject { ["vct"] = "mandate.checkout.1", ["checkout_jwt"] = "a.b.c", ["checkout_hash"] = "h" },
            ["checkout_jwt", "checkout_hash"],
            key);
        Assert.True(SdJwt.TryParse(token, out var sdJwt));

        // Act
        var claims = sdJwt.ResolveClaims();
        var item = claims["delegate_payload"].AsArray().Single().AsObject();

        // Assert
        Assert.False(claims.ContainsKey("_sd_alg"));
        Assert.False(item.ContainsKey("_sd"));
        Assert.Equal("a.b.c", item["checkout_jwt"].GetValue<string>());
        Assert.Equal("h", item["checkout_hash"].GetValue<string>());
    }

    [Fact]
    public void ResolveClaims_UndisclosedArrayElement_IsRemoved()
    {
        // Arrange
        var disclosed = SdJwtDisclosure.Create("salt-1", null, "visible");
        var hidden = SdJwtDisclosure.Create("salt-2", null, "hidden");
        var payload = new JsonObject
        {
            ["list"] = new JsonArray(new JsonObject { ["..."] = disclosed.Digest }, new JsonObject { ["..."] = hidden.Digest }, "plain"),
        };
        var token = Ap2TestMandates.Sign(new JsonObject { ["alg"] = "ES256" }, payload, Ap2TestMandates.NewKey("k")) + "~" + disclosed.Encoded + "~";
        Assert.True(SdJwt.TryParse(token, out var sdJwt));

        // Act
        var claims = sdJwt.ResolveClaims();

        // Assert
        Assert.Equal(["visible", "plain"], claims["list"].AsArray().Select(node => node.GetValue<string>()));
    }

    [Fact]
    public void ResolveClaims_UnreferencedDisclosure_Throws()
    {
        // Arrange
        var stray = SdJwtDisclosure.Create("salt-3", "name", "value");
        var token = Ap2TestMandates.Sign(new JsonObject { ["alg"] = "ES256" }, new JsonObject { ["a"] = 1 }, Ap2TestMandates.NewKey("k")) + "~" + stray.Encoded + "~";
        Assert.True(SdJwt.TryParse(token, out var sdJwt));

        // Act and Assert
        Assert.Throws<FormatException>(() => sdJwt.ResolveClaims());
        Assert.False(sdJwt.TryResolveClaims(out _, out var error));
        Assert.Contains("not referenced", error);
    }

    [Fact]
    public void SdJwtDisclosure_Rfc9901Example_HasTheSpecificationDigest()
    {
        // Arrange
        var encoded = "WyJfMjZiYzRMVC1hYzZxMktJNmNCVzVlcyIsICJmYW1pbHlfbmFtZSIsICJNw7ZiaXVzIl0";

        // Act
        var parsed = SdJwtDisclosure.TryParse(encoded, out var disclosure);

        // Assert
        Assert.True(parsed);
        Assert.Equal("_26bc4LT-ac6q2KI6cBW5es", disclosure.Salt);
        Assert.Equal("family_name", disclosure.ClaimName);
        Assert.Equal("Möbius", disclosure.Value.GetValue<string>());
        Assert.Equal("X9yH0Ajrdm1Oij4tWso9UzzKJvPoDxwmuEcO3XAdRC0", disclosure.Digest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("~abc~")]
    [InlineData("a.b.c")]
    [InlineData("eyJhbGciOiJFUzI1NiJ9.e30.sig~not-a-disclosure~")]
    public void TryParse_Malformed_ReturnsFalse(string compact)
    {
        Assert.False(SdJwt.TryParse(compact, out _));
    }
}
