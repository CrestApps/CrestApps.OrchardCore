using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Twilio;

namespace CrestApps.OrchardCore.Tests.Omnichannel;

/// <summary>
/// Twilio signs a JSON webhook's URL, which carries <c>bodySHA256</c>: the plain SHA-256 of the raw body, in lower-case
/// hex (Twilio's own <c>RequestValidator.ValidateBody</c>). It is not an HMAC with the auth token.
/// </summary>
public sealed class TwilioBodyHashTests
{
    private const string Body = "{\"MessageSid\":\"SM123\",\"Body\":\"hello\"}";

    [Fact]
    public void ValidateBody_AcceptsThePlainSha256OfTheBody()
    {
        var validator = new TwilioRequestValidator("auth-token");
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Body)));

        Assert.True(validator.ValidateBody(Body, expected));
    }

    [Fact]
    public void ValidateBody_RefusesAnotherBody()
    {
        var validator = new TwilioRequestValidator("auth-token");
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Body)));

        Assert.False(validator.ValidateBody(Body + " ", expected));
        Assert.False(validator.ValidateBody(Body, string.Empty));
    }
}
