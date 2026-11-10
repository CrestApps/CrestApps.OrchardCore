using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// Builds AP2 v0.2 mandates the way the AP2 Python SDK does (<c>MandateClient.create</c> and <c>present</c>): each
/// token's claims sit in a <c>delegate_payload</c> array whose element is an array-element disclosure, and
/// selectively disclosable fields of that element are object-property disclosures referenced from its <c>_sd</c>.
/// </summary>
internal static class Ap2TestMandates
{
    public const long Now = 1_800_000_000;

    public const string Audience = "merchant";

    public const string Nonce = "b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4";

    public static EcJsonWebKey NewKey(string kid)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return EcJsonWebKey.FromECDsa(ecdsa, kid, includePrivateKey: true);
    }

    public static EcJsonWebKey PublicOf(EcJsonWebKey key)
    {
        return EcJsonWebKey.FromJson(key.ToJson());
    }

    public static JsonObject SampleCheckout(string merchantId = "m-1", string sku = "SKU-A", int quantity = 2)
    {
        return new JsonObject
        {
            ["id"] = "chk_test",
            ["status"] = "ready_for_complete",
            ["currency"] = "USD",
            ["merchant"] = new JsonObject
            {
                ["id"] = merchantId,
                ["name"] = "Store",
                ["website"] = "https://store.example",
            },
            ["line_items"] = new JsonArray(new JsonObject
            {
                ["id"] = "li_1",
                ["item"] = new JsonObject
                {
                    ["id"] = sku,
                    ["title"] = "Widget",
                    ["price"] = 500,
                },
                ["quantity"] = quantity,
                ["totals"] = new JsonArray(),
            }),
            ["totals"] = new JsonArray(new JsonObject { ["type"] = "total", ["amount"] = 500 * quantity }),
            ["links"] = new JsonArray(),
        };
    }

    public static string CreateCheckoutJwt(JsonObject checkout, EcJsonWebKey merchantKey)
    {
        return JwtEs256.Create(checkout, merchantKey);
    }

    public static JsonArray AllowedMerchantsConstraint(params string[] merchantIds)
    {
        var allowed = new JsonArray();

        foreach (var id in merchantIds)
        {
            allowed.Add(new JsonObject { ["id"] = id, ["name"] = "Store " + id });
        }

        return new JsonArray(new JsonObject { ["type"] = "checkout.allowed_merchants", ["allowed"] = allowed });
    }

    public static JsonObject LineItemsConstraint(string sku, int quantity)
    {
        return new JsonObject
        {
            ["type"] = "checkout.line_items",
            ["items"] = new JsonArray(new JsonObject
            {
                ["id"] = "req-1",
                ["acceptable_items"] = new JsonArray(new JsonObject { ["id"] = sku, ["title"] = "Widget" }),
                ["quantity"] = quantity,
            }),
        };
    }

    public static string CreateOpenMandate(
        EcJsonWebKey userKey,
        EcJsonWebKey agentKey,
        JsonArray constraints = null,
        long? exp = null,
        string vct = Ap2MandateTypes.OpenCheckoutMandate)
    {
        var content = new JsonObject
        {
            ["vct"] = vct,
            ["constraints"] = constraints ?? new JsonArray(),
            ["iat"] = Now - 60,
        };

        if (agentKey is not null)
        {
            content["cnf"] = new JsonObject { ["jwk"] = PublicOf(agentKey).ToJson() };
        }

        if (exp.HasValue)
        {
            content["exp"] = exp.Value;
        }

        var header = new JsonObject { ["alg"] = "ES256", ["kid"] = userKey.Kid };

        return Issue(header, new JsonObject(), content, [], userKey);
    }

    public static string PresentClosedMandate(
        string openToken,
        EcJsonWebKey agentKey,
        string checkoutJwt,
        string aud = Audience,
        string nonce = Nonce,
        string typ = Ap2MandateTypes.TerminalHopType,
        string vct = Ap2MandateTypes.CheckoutMandate,
        JsonObject extraContent = null)
    {
        Assert.True(SdJwt.TryParse(openToken, out var previous));

        var content = new JsonObject
        {
            ["vct"] = vct,
            ["checkout_jwt"] = checkoutJwt,
            ["checkout_hash"] = Ap2MandateVerifier.ComputeCheckoutHash(checkoutJwt),
        };

        if (extraContent is not null)
        {
            foreach (var property in extraContent)
            {
                content[property.Key] = property.Value?.DeepClone();
            }
        }

        var claims = new JsonObject
        {
            ["iat"] = Now - 5,
            ["aud"] = aud,
            ["nonce"] = nonce,
            ["sd_hash"] = previous.ComputeSdHash(),
        };

        var header = new JsonObject { ["alg"] = "ES256", ["typ"] = typ, ["kid"] = agentKey.Kid };
        var closed = Issue(header, claims, content, ["checkout_jwt"], agentKey);

        return openToken.TrimEnd('~') + "~~" + closed;
    }

    public static string Issue(JsonObject header, JsonObject claims, JsonObject delegateContent, IEnumerable<string> selectivelyDisclosed, EcJsonWebKey signer)
    {
        var item = delegateContent.DeepClone().AsObject();
        var disclosures = new List<SdJwtDisclosure>();
        var digests = new JsonArray();

        foreach (var field in selectivelyDisclosed)
        {
            var disclosure = SdJwtDisclosure.Create(NextSalt(), field, item[field]);
            item.Remove(field);
            disclosures.Add(disclosure);
            digests.Add(disclosure.Digest);
        }

        if (digests.Count > 0)
        {
            item["_sd"] = digests;
        }

        var itemDisclosure = SdJwtDisclosure.Create(NextSalt(), null, item);
        disclosures.Insert(0, itemDisclosure);

        var payload = new JsonObject
        {
            ["delegate_payload"] = new JsonArray(new JsonObject { ["..."] = itemDisclosure.Digest }),
        };

        foreach (var claim in claims)
        {
            payload[claim.Key] = claim.Value?.DeepClone();
        }

        payload["_sd_alg"] = "sha-256";

        return Sign(header, payload, signer) + "~" + string.Join("~", disclosures.Select(d => d.Encoded)) + "~";
    }

    public static string Sign(JsonObject header, JsonObject payload, EcJsonWebKey signer)
    {
        using var ecdsa = signer.CreateECDsa();

        return JwsSigner.SignCompact(header, Encoding.UTF8.GetBytes(payload.ToJsonString()), ecdsa);
    }

    public static string ToBase64Url(string json)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static Ap2VerificationOptions Options(EcJsonWebKey trustedRoot)
    {
        var publicRoot = trustedRoot is null
            ? null
            : PublicOf(trustedRoot);

        return new Ap2VerificationOptions
        {
            RootKeyResolver = context => context["header"]?["kid"]?.GetValue<string>() == publicRoot?.Kid
                ? publicRoot
                : null,
            ExpectedAudience = Audience,
            ExpectedNonce = Nonce,
            Now = DateTimeOffset.FromUnixTimeSeconds(Now),
        };
    }

    private static string NextSalt()
    {
        return Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
    }
}
