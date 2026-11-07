using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Core.Signatures;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks;

public sealed class WebhookSignatureValidatorTests
{
    [Fact]
    public void HmacSha256Hex_AcceptsTheSendersSignature_AndRefusesATamperedBody()
    {
        // Arrange
        var expected = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("secret"), Encoding.UTF8.GetBytes("{\"a\":1}")));

        // Act
        var valid = HmacWebhookSignatureValidator.Sha256Hex.IsValid(Context("secret", expected.ToUpperInvariant(), "{\"a\":1}"));
        var tampered = HmacWebhookSignatureValidator.Sha256Hex.IsValid(Context("secret", expected, "{\"a\":2}"));

        // Assert
        Assert.True(valid);
        Assert.False(tampered);
    }

    [Fact]
    public void HmacSha1Base64_MatchesTwilioStyleSignatures()
    {
        // Arrange
        var signedContent = "https://example.com/sms/twilioBody=HiFrom+15551234567";
#pragma warning disable CA5350 // Twilio signs with HMAC-SHA1; the test reproduces its signature.
        var expected = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes("token"), Encoding.UTF8.GetBytes(signedContent)));
#pragma warning restore CA5350

        // Act
        var valid = HmacWebhookSignatureValidator.Sha1Base64.IsValid(Context("token", expected, signedContent));
        var wrongSecret = HmacWebhookSignatureValidator.Sha1Base64.IsValid(Context("other", expected, signedContent));

        // Assert
        Assert.True(valid);
        Assert.False(wrongSecret);
    }

    [Fact]
    public void Prefix_IsRequired_AndComparedWithoutRegardToCase()
    {
        // Arrange
        var validator = new HmacWebhookSignatureValidator("easypost", HashAlgorithmName.SHA256, WebhookSignatureEncoding.Hex, "hmac-sha256-hex=");
        var signature = validator.Compute("secret", "body");

        // Act
        var valid = validator.IsValid(Context("secret", signature.ToUpperInvariant(), "body"));
        var missingPrefix = validator.IsValid(Context("secret", signature["hmac-sha256-hex=".Length..], "body"));

        // Assert
        Assert.True(valid);
        Assert.False(missingPrefix);
    }

    [Theory]
    [InlineData(null, "sig")]
    [InlineData("secret", null)]
    [InlineData("", "")]
    public void MissingSecretOrSignature_NeverValidates(string secret, string signature)
    {
        Assert.False(HmacWebhookSignatureValidator.Sha256Hex.IsValid(Context(secret, signature, "body")));
        Assert.False(new SharedKeyWebhookSignatureValidator().IsValid(Context(secret, signature, "body")));
    }

    [Fact]
    public void SharedKey_ComparesTheKeyItself()
    {
        var validator = new SharedKeyWebhookSignatureValidator();

        Assert.True(validator.IsValid(Context("key", "key", null)));
        Assert.False(validator.IsValid(Context("key", "kez", null)));
    }

    [Fact]
    public void AddProviderWebhookInbox_RegistersEachBuiltInValidatorOnce_AndTheIdentityNormalizer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddProviderWebhookInbox();
        services.AddProviderWebhookInbox();

        using var provider = services.BuildServiceProvider();
        var schemes = provider.GetServices<IWebhookSignatureValidator>().Select(validator => validator.Scheme).ToArray();

        // Assert
        Assert.Equal(["hmac-sha256-hex", "hmac-sha1-base64", "shared-key"], schemes);
        Assert.IsType<DefaultWebhookProviderNameNormalizer>(provider.GetRequiredService<IWebhookProviderNameNormalizer>());
        Assert.Equal("Alias", provider.GetRequiredService<IWebhookProviderNameNormalizer>().Normalize("Alias"));
    }

    [Fact]
    public void ProviderIdentityResolver_NormalizesInboxProviderNamesThroughItsAliases()
    {
        // Arrange
        var resolver = new ProviderIdentityResolver([new AliasIdentityProvider()]);

        // Act and assert
        Assert.Equal("Canonical", Normalize(resolver, "alias"));
        Assert.Equal("unknown", Normalize(resolver, "unknown"));
    }

    // The inbox only ever sees the resolver as its normalizer; the cast proves that contract, not the class method.
    private static string Normalize(ProviderIdentityResolver resolver, string providerName)
        => ((IWebhookProviderNameNormalizer)resolver).Normalize(providerName);

    private static WebhookSignatureValidationContext Context(string secret, string signature, string signedContent)
        => new()
        {
            Secret = secret,
            Signature = signature,
            SignedContent = signedContent,
        };

    private sealed class AliasIdentityProvider : IProviderIdentityProvider
    {
        public IEnumerable<ProviderIdentity> GetIdentities()
            => [new ProviderIdentity("Canonical", "alias")];
    }
}
