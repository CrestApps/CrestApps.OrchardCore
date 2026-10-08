using System.Text;
using CrestApps.OrchardCore.Telnyx.Services;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// This validator is the only thing standing between the public webhook endpoint and anybody who can guess the
/// URL. Nothing covered it, so a refactor that made it accept a tampered body would have shipped silently and
/// let a stranger hang up calls, mark them answered, or replay a recording notification.
/// </summary>
public sealed class TelnyxWebhookSignatureValidatorTests
{
    [Fact]
    public void Validate_AcceptsASignatureOverTheExactTimestampAndBody()
    {
        // Arrange
        var key = GenerateKey();
        const string Timestamp = "1772636400";
        const string Body = """{"data":{"event_type":"call.answered"}}""";

        // Act
        var valid = TelnyxWebhookSignatureValidator.TryValidate(
            key.PublicKeyBase64,
            Sign(key, Timestamp, Body),
            Timestamp,
            Body);

        // Assert
        Assert.True(valid);
    }

    [Fact]
    public void Validate_RefusesATamperedBody()
    {
        // Arrange
        // The attack this exists to stop: a real signed delivery, replayed with the payload changed to hang up
        // somebody else's call.
        var key = GenerateKey();
        const string Timestamp = "1772636400";
        var signature = Sign(key, Timestamp, """{"data":{"event_type":"call.answered"}}""");

        // Act
        var valid = TelnyxWebhookSignatureValidator.TryValidate(
            key.PublicKeyBase64,
            signature,
            Timestamp,
            """{"data":{"event_type":"call.hangup"}}""");

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void Validate_RefusesASignatureReusedWithADifferentTimestamp()
    {
        // Arrange
        // The timestamp is inside the signed payload precisely so a captured delivery cannot be replayed later
        // with a fresh header to get past a staleness check.
        var key = GenerateKey();
        const string Body = """{"data":{"event_type":"call.answered"}}""";
        var signature = Sign(key, "1772636400", Body);

        // Act
        var valid = TelnyxWebhookSignatureValidator.TryValidate(key.PublicKeyBase64, signature, "1772640000", Body);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void Validate_RefusesASignatureFromADifferentKey()
    {
        // Arrange
        // Somebody with their own Telnyx account signing deliveries at this tenant's endpoint.
        var ours = GenerateKey();
        var theirs = GenerateKey();
        const string Timestamp = "1772636400";
        const string Body = """{"data":{"event_type":"call.answered"}}""";

        // Act
        var valid = TelnyxWebhookSignatureValidator.TryValidate(
            ours.PublicKeyBase64,
            Sign(theirs, Timestamp, Body),
            Timestamp,
            Body);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public void Validate_RefusesAKeyOfTheWrongLength()
    {
        // Arrange
        // A truncated or padded key is a misconfiguration. Refusing is right: the alternative is an exception
        // deep in the crypto library on every inbound webhook.
        const string Timestamp = "1772636400";
        const string Body = "{}";

        // Act
        var valid = TelnyxWebhookSignatureValidator.TryValidate(
            Convert.ToBase64String(new byte[16]),
            Convert.ToBase64String(new byte[64]),
            Timestamp,
            Body);

        // Assert
        Assert.False(valid);
    }

    [Theory]
    [InlineData(null, "c2ln", "1772636400", "{}")]
    [InlineData("", "c2ln", "1772636400", "{}")]
    [InlineData("a2V5", null, "1772636400", "{}")]
    [InlineData("a2V5", "c2ln", null, "{}")]
    [InlineData("a2V5", "c2ln", "1772636400", null)]
    [InlineData("not base64!", "c2ln", "1772636400", "{}")]
    [InlineData("a2V5", "not base64!", "1772636400", "{}")]
    public void Validate_RefusesAnythingItCannotFullyCheck(string publicKey, string signature, string timestamp, string body)
    {
        // Assert
        // Every missing or malformed input is a refusal rather than a pass, because the one thing a signature
        // check must never do is let something through it did not actually verify.
        Assert.False(TelnyxWebhookSignatureValidator.TryValidate(publicKey, signature, timestamp, body));
    }

    [Fact]
    public void Validate_AcceptsAnEmptyBody_WhenThatIsWhatWasSigned()
    {
        // Arrange
        // An empty body is a legitimate thing to sign; treating it as missing input would refuse a valid
        // delivery.
        var key = GenerateKey();
        const string Timestamp = "1772636400";

        // Act
        var valid = TelnyxWebhookSignatureValidator.TryValidate(
            key.PublicKeyBase64,
            Sign(key, Timestamp, string.Empty),
            Timestamp,
            string.Empty);

        // Assert
        Assert.True(valid);
    }

    private static (string PublicKeyBase64, Ed25519PrivateKeyParameters PrivateKey) GenerateKey()
    {
        var random = new SecureRandom();
        var privateKey = new Ed25519PrivateKeyParameters(random);

        return (Convert.ToBase64String(privateKey.GeneratePublicKey().GetEncoded()), privateKey);
    }

    private static string Sign((string PublicKeyBase64, Ed25519PrivateKeyParameters PrivateKey) key, string timestamp, string body)
    {
        var payload = Encoding.UTF8.GetBytes($"{timestamp}|{body}");
        var signer = new Ed25519Signer();
        signer.Init(true, key.PrivateKey);
        signer.BlockUpdate(payload, 0, payload.Length);

        return Convert.ToBase64String(signer.GenerateSignature());
    }
}
