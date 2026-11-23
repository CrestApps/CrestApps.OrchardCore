using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

public sealed class MailchimpWebhookSignatureTests
{
    private const string Secret = "whsec-signing-secret";
    private const string Body = "type=unsubscribe&fired_at=2026-11-22+10%3A00%3A00&data%5Bemail%5D=a%40example.com";

    private static readonly DateTime _nowUtc = new(2026, 11, 22, 10, 0, 30, DateTimeKind.Utc);

    [Fact]
    public void Verify_ASignatureMadeWithTheSecretOverTheTimeAndTheRawBody_IsValid()
    {
        // Arrange
        var timestamp = UnixSeconds(_nowUtc.AddSeconds(-20));
        var header = $"t={timestamp},v1={Sign(Secret, $"{timestamp}.{Body}")}";

        // Act
        var result = MailchimpWebhookSignature.Verify(header, Body, Secret, _nowUtc);

        // Assert
        Assert.Equal(MailchimpSignatureResult.Valid, result);
        Assert.Equal(Sign(Secret, $"{timestamp}.{Body}"), MailchimpWebhookSignature.Compute(Secret, timestamp, Body));
    }

    [Fact]
    public void Verify_AcceptsAnyOfSeveralSignatures_AndSpacesAndUpperCaseHex()
    {
        // Arrange
        var timestamp = UnixSeconds(_nowUtc);
        var header = $" t={timestamp}, v1=deadbeef , v1={Sign(Secret, $"{timestamp}.{Body}").ToUpperInvariant()} ";

        // Act
        var result = MailchimpWebhookSignature.Verify(header, Body, Secret, _nowUtc);

        // Assert
        Assert.Equal(MailchimpSignatureResult.Valid, result);
    }

    [Fact]
    public void Verify_ASignatureMadeWithAnotherSecret_IsInvalid()
    {
        // Arrange
        var timestamp = UnixSeconds(_nowUtc);
        var header = $"t={timestamp},v1={Sign("another-secret", $"{timestamp}.{Body}")}";

        // Act
        var result = MailchimpWebhookSignature.Verify(header, Body, Secret, _nowUtc);

        // Assert
        Assert.Equal(MailchimpSignatureResult.Invalid, result);
    }

    [Fact]
    public void Verify_AChangedBody_IsInvalid()
    {
        // Arrange
        var timestamp = UnixSeconds(_nowUtc);
        var header = $"t={timestamp},v1={Sign(Secret, $"{timestamp}.{Body}")}";

        // Act
        var result = MailchimpWebhookSignature.Verify(header, Body.Replace("unsubscribe", "subscribe", StringComparison.Ordinal), Secret, _nowUtc);

        // Assert
        Assert.Equal(MailchimpSignatureResult.Invalid, result);
    }

    [Fact]
    public void Verify_AChangedTimestamp_IsInvalid()
    {
        // Arrange
        var timestamp = UnixSeconds(_nowUtc);
        var header = $"t={UnixSeconds(_nowUtc.AddSeconds(1))},v1={Sign(Secret, $"{timestamp}.{Body}")}";

        // Act
        var result = MailchimpWebhookSignature.Verify(header, Body, Secret, _nowUtc);

        // Assert
        Assert.Equal(MailchimpSignatureResult.Invalid, result);
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(-3600)]
    [InlineData(301)]
    public void Verify_ARightSignatureMoreThanFiveMinutesFromNow_IsStale(int offsetSeconds)
    {
        // Arrange
        var timestamp = UnixSeconds(_nowUtc.AddSeconds(offsetSeconds));
        var header = $"t={timestamp},v1={Sign(Secret, $"{timestamp}.{Body}")}";

        // Act
        var result = MailchimpWebhookSignature.Verify(header, Body, Secret, _nowUtc);

        // Assert
        Assert.Equal(MailchimpSignatureResult.Stale, result);
    }

    [Fact]
    public void Verify_ARightSignatureJustInsideFiveMinutes_IsValid()
    {
        // Arrange
        var timestamp = UnixSeconds(_nowUtc.AddSeconds(-299));
        var header = $"t={timestamp},v1={Sign(Secret, $"{timestamp}.{Body}")}";

        // Act
        var result = MailchimpWebhookSignature.Verify(header, Body, Secret, _nowUtc);

        // Assert
        Assert.Equal(MailchimpSignatureResult.Valid, result);
    }

    [Theory]
    [InlineData(null, MailchimpSignatureResult.Missing)]
    [InlineData("", MailchimpSignatureResult.Missing)]
    [InlineData("v1=abc", MailchimpSignatureResult.Malformed)]
    [InlineData("t=1764000000", MailchimpSignatureResult.Malformed)]
    [InlineData("t=yesterday,v1=abc", MailchimpSignatureResult.Malformed)]
    [InlineData("t=-5,v1=abc", MailchimpSignatureResult.Malformed)]
    [InlineData("t=99999999999999999,v1=abc", MailchimpSignatureResult.Malformed)]
    [InlineData("sha256=abc", MailchimpSignatureResult.Malformed)]
    public void Verify_AMissingOrMalformedHeader_IsRefused(string header, MailchimpSignatureResult expected)
    {
        // Act
        var result = MailchimpWebhookSignature.Verify(header, Body, Secret, _nowUtc);

        // Assert
        Assert.Equal(expected, result);
    }

    private static string UnixSeconds(DateTime utc)
        => new DateTimeOffset(utc).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    private static string Sign(string secret, string content)
        => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(content)));
}
