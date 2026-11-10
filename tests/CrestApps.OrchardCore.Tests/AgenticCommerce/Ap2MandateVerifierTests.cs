using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// AP2 v0.2 checkout mandate chains verify the way the AP2 SDK verifies them, and every broken link fails with the
/// UCP error code for it.
/// </summary>
public sealed class Ap2MandateVerifierTests
{
    private readonly EcJsonWebKey _userKey = Ap2TestMandates.NewKey("user-key-1");
    private readonly EcJsonWebKey _agentKey = Ap2TestMandates.NewKey("agent-key-1");
    private readonly EcJsonWebKey _merchantKey = Ap2TestMandates.NewKey("merchant-key-1");
    private readonly Ap2MandateVerifier _verifier = new();

    [Fact]
    public void VerifyCheckoutMandate_HumanNotPresentChain_Succeeds()
    {
        // Arrange
        var constraints = Ap2TestMandates.AllowedMerchantsConstraint("m-1", "m-2");
        constraints.Add(Ap2TestMandates.LineItemsConstraint("SKU-A", 2));
        var open = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey, constraints, exp: Ap2TestMandates.Now + 3600);
        var checkoutJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey);
        var mandate = Ap2TestMandates.PresentClosedMandate(open, _agentKey, checkoutJwt);
        var options = Ap2TestMandates.Options(_userKey);
        options.MerchantKeyResolver = kid => kid == "merchant-key-1" ? Ap2TestMandates.PublicOf(_merchantKey) : null;
        options.ExpectedCheckoutHash = Ap2MandateVerifier.ComputeCheckoutHash(checkoutJwt);

        // Act
        var result = _verifier.VerifyCheckoutMandate(mandate, options);

        // Assert
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(checkoutJwt, result.CheckoutJwt);
        Assert.Equal(Ap2MandateVerifier.ComputeCheckoutHash(checkoutJwt), result.CheckoutHash);
        Assert.Equal("chk_test", result.Checkout["id"].GetValue<string>());
        Assert.Equal("mandate.checkout.1", result.ClosedMandate["vct"].GetValue<string>());
        Assert.Equal("mandate.checkout.open.1", result.Constraints["vct"].GetValue<string>());
        Assert.Equal(2, result.Constraints["constraints"].AsArray().Count);

        var closedJwt = mandate.Substring(mandate.LastIndexOf("~~", StringComparison.Ordinal) + 2).Split('~')[0];
        Assert.Equal(Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(closedJwt))), result.ClosedMandateHash);
        Assert.True(SdJwt.TryParse(open, out var openToken));
        Assert.Equal(openToken.ComputeSdHash(), result.OpenMandateHash);
    }

    [Fact]
    public void VerifyCheckoutMandate_TamperedClosedPayload_FailsWithInvalidSignature()
    {
        // Arrange
        var mandate = CreateMandate();
        var closedJwt = mandate.Substring(mandate.LastIndexOf("~~", StringComparison.Ordinal) + 2).Split('~')[0];
        var closedParts = closedJwt.Split('.');
        var payload = JsonNode.Parse(Base64Url.DecodeFromChars(closedParts[1])).AsObject();
        payload["iat"] = Ap2TestMandates.Now - 1;
        var tamperedJwt = closedParts[0] + "." + Ap2TestMandates.ToBase64Url(payload.ToJsonString()) + "." + closedParts[2];
        var tampered = mandate.Replace(closedJwt, tamperedJwt);

        // Act
        var result = _verifier.VerifyCheckoutMandate(tampered, Ap2TestMandates.Options(_userKey));

        // Assert
        Assert.Equal(Ap2ErrorCodes.MandateInvalidSignature, result.ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_SwappedCheckoutDisclosure_FailsWithInvalidSignature()
    {
        // Arrange
        var mandate = CreateMandate();
        var otherCheckoutJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(quantity: 9), _merchantKey);
        var swapped = SdJwtDisclosure.Create("salt-swapped", "checkout_jwt", otherCheckoutJwt);
        var segments = mandate.Split('~');
        segments[segments.Length - 2] = swapped.Encoded;

        // Act
        var result = _verifier.VerifyCheckoutMandate(string.Join("~", segments), Ap2TestMandates.Options(_userKey));

        // Assert
        Assert.Equal(Ap2ErrorCodes.MandateInvalidSignature, result.ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_ClosedHopSignedByAnotherKey_FailsWithInvalidSignature()
    {
        // Arrange
        var open = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey);
        var checkoutJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey);
        var mandate = Ap2TestMandates.PresentClosedMandate(open, Ap2TestMandates.NewKey("intruder"), checkoutJwt);

        // Act
        var result = _verifier.VerifyCheckoutMandate(mandate, Ap2TestMandates.Options(_userKey));

        // Assert
        Assert.Equal(Ap2ErrorCodes.MandateInvalidSignature, result.ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_RootSignedByAnUntrustedKey_FailsWithInvalidSignature()
    {
        // Arrange
        var mandate = CreateMandate();
        var impostor = Ap2TestMandates.NewKey("user-key-1");

        // Act
        var result = _verifier.VerifyCheckoutMandate(mandate, Ap2TestMandates.Options(impostor));

        // Assert
        Assert.Equal(Ap2ErrorCodes.MandateInvalidSignature, result.ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_ExpiredOpenMandate_FailsWithExpired()
    {
        // Arrange
        var open = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey, exp: Ap2TestMandates.Now - 120);
        var mandate = Ap2TestMandates.PresentClosedMandate(open, _agentKey, Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey));

        // Act
        var result = _verifier.VerifyCheckoutMandate(mandate, Ap2TestMandates.Options(_userKey));
        var withinSkew = Ap2TestMandates.Options(_userKey);
        withinSkew.ClockSkew = TimeSpan.FromSeconds(300);

        // Assert
        Assert.Equal(Ap2ErrorCodes.MandateExpired, result.ErrorCode);
        Assert.True(_verifier.VerifyCheckoutMandate(mandate, withinSkew).Succeeded);
    }

    [Fact]
    public void VerifyCheckoutMandate_WrongAudienceOrNonce_FailsWithScopeMismatch()
    {
        // Arrange
        var open = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey);
        var checkoutJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey);
        var otherAudience = Ap2TestMandates.PresentClosedMandate(open, _agentKey, checkoutJwt, aud: "another-merchant");
        var otherNonce = Ap2TestMandates.PresentClosedMandate(open, _agentKey, checkoutJwt, nonce: "replayed-nonce");

        // Act
        var audienceResult = _verifier.VerifyCheckoutMandate(otherAudience, Ap2TestMandates.Options(_userKey));
        var nonceResult = _verifier.VerifyCheckoutMandate(otherNonce, Ap2TestMandates.Options(_userKey));

        // Assert
        Assert.Equal(Ap2ErrorCodes.MandateScopeMismatch, audienceResult.ErrorCode);
        Assert.Equal(Ap2ErrorCodes.MandateScopeMismatch, nonceResult.ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_WrongMandateTypes_FailWithScopeMismatch()
    {
        // Arrange
        var checkoutJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey);
        var paymentOpen = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey, vct: Ap2MandateTypes.OpenPaymentMandate);
        var wrongOpen = Ap2TestMandates.PresentClosedMandate(paymentOpen, _agentKey, checkoutJwt);
        var open = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey);
        var wrongClosed = Ap2TestMandates.PresentClosedMandate(open, _agentKey, checkoutJwt, vct: Ap2MandateTypes.PaymentMandate);

        // Act
        var openResult = _verifier.VerifyCheckoutMandate(wrongOpen, Ap2TestMandates.Options(_userKey));
        var closedResult = _verifier.VerifyCheckoutMandate(wrongClosed, Ap2TestMandates.Options(_userKey));

        // Assert
        Assert.Equal(Ap2ErrorCodes.MandateScopeMismatch, openResult.ErrorCode);
        Assert.Equal(Ap2ErrorCodes.MandateScopeMismatch, closedResult.ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_MissingKeys_FailWithAgentMissingKey()
    {
        // Arrange
        var mandate = CreateMandate();
        var openWithoutAgentKey = Ap2TestMandates.CreateOpenMandate(_userKey, agentKey: null);
        var withoutCnf = Ap2TestMandates.PresentClosedMandate(
            openWithoutAgentKey,
            _agentKey,
            Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey));

        // Act
        var untrustedRoot = _verifier.VerifyCheckoutMandate(mandate, Ap2TestMandates.Options(null));
        var noConfirmationKey = _verifier.VerifyCheckoutMandate(withoutCnf, Ap2TestMandates.Options(_userKey));

        // Assert
        Assert.Equal(Ap2ErrorCodes.AgentMissingKey, untrustedRoot.ErrorCode);
        Assert.Equal(Ap2ErrorCodes.AgentMissingKey, noConfirmationKey.ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_ConstraintViolations_FailWithScopeMismatch()
    {
        // Arrange
        var checkoutJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(merchantId: "m-evil"), _merchantKey);
        var merchantOpen = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey, Ap2TestMandates.AllowedMerchantsConstraint("m-1"));
        var wrongMerchant = Ap2TestMandates.PresentClosedMandate(merchantOpen, _agentKey, checkoutJwt);

        var tooManyJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(quantity: 3), _merchantKey);
        var lineItemOpen = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey, new JsonArray(Ap2TestMandates.LineItemsConstraint("SKU-A", 2)));
        var tooMany = Ap2TestMandates.PresentClosedMandate(lineItemOpen, _agentKey, tooManyJwt);

        var otherSkuJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(sku: "SKU-B", quantity: 1), _merchantKey);
        var otherSku = Ap2TestMandates.PresentClosedMandate(lineItemOpen, _agentKey, otherSkuJwt);

        var unknownOpen = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey, new JsonArray(new JsonObject { ["type"] = "checkout.unknown" }));
        var unknown = Ap2TestMandates.PresentClosedMandate(unknownOpen, _agentKey, otherSkuJwt);

        // Act and Assert
        Assert.Equal(Ap2ErrorCodes.MandateScopeMismatch, _verifier.VerifyCheckoutMandate(wrongMerchant, Ap2TestMandates.Options(_userKey)).ErrorCode);
        Assert.Equal(Ap2ErrorCodes.MandateScopeMismatch, _verifier.VerifyCheckoutMandate(tooMany, Ap2TestMandates.Options(_userKey)).ErrorCode);
        Assert.Equal(Ap2ErrorCodes.MandateScopeMismatch, _verifier.VerifyCheckoutMandate(otherSku, Ap2TestMandates.Options(_userKey)).ErrorCode);
        Assert.Equal(Ap2ErrorCodes.MandateScopeMismatch, _verifier.VerifyCheckoutMandate(unknown, Ap2TestMandates.Options(_userKey)).ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_StructuralProblems_FailWithMandateInvalid()
    {
        // Arrange
        var open = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey);
        var checkoutJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey);
        var intermediateLast = Ap2TestMandates.PresentClosedMandate(
            open,
            _agentKey,
            checkoutJwt,
            typ: Ap2MandateTypes.IntermediateHopType,
            extraContent: new JsonObject { ["cnf"] = new JsonObject { ["jwk"] = Ap2TestMandates.PublicOf(_agentKey).ToJson() } });
        var wrongTyp = Ap2TestMandates.PresentClosedMandate(open, _agentKey, checkoutJwt, typ: "JWT");

        // Act and Assert
        Assert.Equal(Ap2ErrorCodes.MandateRequired, _verifier.VerifyCheckoutMandate(" ", Ap2TestMandates.Options(_userKey)).ErrorCode);
        Assert.Equal(Ap2ErrorCodes.MandateInvalid, _verifier.VerifyCheckoutMandate("not~~a~mandate", Ap2TestMandates.Options(_userKey)).ErrorCode);
        Assert.Equal(Ap2ErrorCodes.MandateInvalid, _verifier.VerifyCheckoutMandate(intermediateLast, Ap2TestMandates.Options(_userKey)).ErrorCode);
        Assert.Equal(Ap2ErrorCodes.MandateInvalid, _verifier.VerifyCheckoutMandate(wrongTyp, Ap2TestMandates.Options(_userKey)).ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_CheckoutSignedByAnotherMerchantKey_FailsWithMerchantAuthorizationInvalid()
    {
        // Arrange
        var mandate = CreateMandate();
        var options = Ap2TestMandates.Options(_userKey);
        var otherMerchant = Ap2TestMandates.NewKey("merchant-key-1");
        options.MerchantKeyResolver = _ => Ap2TestMandates.PublicOf(otherMerchant);

        // Act
        var result = _verifier.VerifyCheckoutMandate(mandate, options);

        // Assert
        Assert.Equal(Ap2ErrorCodes.MerchantAuthorizationInvalid, result.ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_SingleSdJwtWithKeyBinding_Succeeds()
    {
        // Arrange
        var checkoutJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey);
        var holderKey = Ap2TestMandates.NewKey("holder-1");
        var content = new JsonObject
        {
            ["vct"] = Ap2MandateTypes.CheckoutMandate,
            ["checkout_jwt"] = checkoutJwt,
            ["checkout_hash"] = Ap2MandateVerifier.ComputeCheckoutHash(checkoutJwt),
            ["cnf"] = new JsonObject { ["jwk"] = Ap2TestMandates.PublicOf(holderKey).ToJson() },
        };
        var sdJwt = Ap2TestMandates.Issue(new JsonObject { ["alg"] = "ES256", ["kid"] = "user-key-1", ["typ"] = "dc+sd-jwt" }, new JsonObject(), content, ["checkout_jwt"], _userKey);
        Assert.True(SdJwt.TryParse(sdJwt, out var parsed));
        var keyBinding = Ap2TestMandates.Sign(
            new JsonObject { ["alg"] = "ES256", ["typ"] = "kb+jwt" },
            new JsonObject
            {
                ["iat"] = Ap2TestMandates.Now,
                ["aud"] = Ap2TestMandates.Audience,
                ["nonce"] = Ap2TestMandates.Nonce,
                ["sd_hash"] = parsed.ComputeSdHash(),
            },
            holderKey);

        // Act
        var result = _verifier.VerifyCheckoutMandate(sdJwt + keyBinding, Ap2TestMandates.Options(_userKey));
        var unbound = _verifier.VerifyCheckoutMandate(sdJwt, Ap2TestMandates.Options(_userKey));

        // Assert
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Null(result.OpenMandateHash);
        Assert.Null(result.Constraints);
        Assert.Equal(Ap2ErrorCodes.MandateInvalid, unbound.ErrorCode);
    }

    [Fact]
    public void VerifyCheckoutMandate_RootWithX5cChain_TrustsOnlyTheConfiguredRoot()
    {
        // Arrange
        using var caKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var caRequest = new CertificateRequest("CN=Test Credential Issuer CA", caKey, HashAlgorithmName.SHA256);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var caCertificate = caRequest.CreateSelfSigned(DateTimeOffset.FromUnixTimeSeconds(Ap2TestMandates.Now).AddDays(-1), DateTimeOffset.FromUnixTimeSeconds(Ap2TestMandates.Now).AddDays(30));

        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leafRequest = new CertificateRequest("CN=Test Issuer", leafKey, HashAlgorithmName.SHA256);
        using var leafCertificate = leafRequest.Create(caCertificate, DateTimeOffset.FromUnixTimeSeconds(Ap2TestMandates.Now).AddDays(-1), DateTimeOffset.FromUnixTimeSeconds(Ap2TestMandates.Now).AddDays(10), [1, 2, 3, 4]);

        var leafJwk = EcJsonWebKey.FromECDsa(leafKey, null, includePrivateKey: true);
        var header = new JsonObject
        {
            ["alg"] = "ES256",
            ["x5c"] = new JsonArray(Convert.ToBase64String(leafCertificate.RawData)),
        };
        var content = new JsonObject
        {
            ["vct"] = Ap2MandateTypes.OpenCheckoutMandate,
            ["constraints"] = new JsonArray(),
            ["cnf"] = new JsonObject { ["jwk"] = Ap2TestMandates.PublicOf(_agentKey).ToJson() },
        };
        var open = Ap2TestMandates.Issue(header, new JsonObject(), content, [], leafJwk);
        var mandate = Ap2TestMandates.PresentClosedMandate(open, _agentKey, Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey));

        using var otherCaKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherCa = new CertificateRequest("CN=Other CA", otherCaKey, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.FromUnixTimeSeconds(Ap2TestMandates.Now).AddDays(-1), DateTimeOffset.FromUnixTimeSeconds(Ap2TestMandates.Now).AddDays(30));

        var trusted = Ap2TestMandates.Options(null);
        trusted.TrustedRoots = [caCertificate];
        var untrusted = Ap2TestMandates.Options(null);
        untrusted.TrustedRoots = [otherCa];

        // Act
        var trustedResult = _verifier.VerifyCheckoutMandate(mandate, trusted);
        var untrustedResult = _verifier.VerifyCheckoutMandate(mandate, untrusted);

        // Assert
        Assert.True(trustedResult.Succeeded, trustedResult.ErrorMessage);
        Assert.Equal(Ap2ErrorCodes.MandateInvalidSignature, untrustedResult.ErrorCode);
    }

    private string CreateMandate()
    {
        var open = Ap2TestMandates.CreateOpenMandate(_userKey, _agentKey);
        var checkoutJwt = Ap2TestMandates.CreateCheckoutJwt(Ap2TestMandates.SampleCheckout(), _merchantKey);

        return Ap2TestMandates.PresentClosedMandate(open, _agentKey, checkoutJwt);
    }
}
