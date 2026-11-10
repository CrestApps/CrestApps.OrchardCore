using CrestApps.OrchardCore.Stripe.Services;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// A Google Pay credential an agent hands over is charged through Stripe only when it holds a card token Stripe made:
/// a raw token, or Google Pay's JSON with Stripe's token as its id.
/// </summary>
public sealed class StripeGooglePayCardTokenTests
{
    [Theory]
    [InlineData("tok_1Abc", "tok_1Abc")]
    [InlineData("  tok_1Abc  ", "tok_1Abc")]
    [InlineData("""{"id":"tok_1Abc","object":"token","card":{"last4":"4242"}}""", "tok_1Abc")]
    [InlineData("""  {"id":"tok_1Abc"}  """, "tok_1Abc")]
    public void GetGooglePayCardToken_ForAStripeToken_ReturnsIt(string credential, string expected)
    {
        // Act
        var token = StripePaymentIntentService.GetGooglePayCardToken(credential);

        // Assert
        Assert.Equal(expected, token);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"id":"pm_1Abc"}""")]
    [InlineData("""{"id":12}""")]
    [InlineData("""{"signature":"MEUC","protocolVersion":"ECv2","signedMessage":"{}"}""")]
    [InlineData("pm_1Abc")]
    [InlineData("")]
    [InlineData(null)]
    public void GetGooglePayCardToken_WithoutAStripeToken_ReturnsNull(string credential)
    {
        // Act
        var token = StripePaymentIntentService.GetGooglePayCardToken(credential);

        // Assert
        Assert.Null(token);
    }
}
