using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskSettingsUtilitiesTests
{
    [Theory]
    [InlineData("http://asterisk:8088/ari", "ws")]
    [InlineData("https://asterisk:8089/ari", "wss")]
    public void CreateEventsUri_BuildsSubscribeAllEventStream(string baseUrl, string expectedScheme)
    {
        // Arrange
        var settings = new AsteriskResolvedSettings
        {
            BaseUrl = baseUrl,
            UserName = "user",
            Password = "secret",
            ApplicationName = "contact-center",
        };

        // Act
        var uri = AsteriskSettingsUtilities.CreateEventsUri(settings);

        // Assert
        Assert.NotNull(uri);
        Assert.Equal(expectedScheme, uri.Scheme);
        Assert.EndsWith("/ari/events", uri.AbsolutePath);
        Assert.Contains("app=contact-center", uri.Query);
        Assert.Contains("subscribeAll=true", uri.Query);
    }

    [Fact]
    public void CreateEventsUri_WhenBaseUrlMissing_ReturnsNull()
    {
        // Arrange
        var settings = new AsteriskResolvedSettings
        {
            BaseUrl = null,
            ApplicationName = "contact-center",
        };

        // Act
        var uri = AsteriskSettingsUtilities.CreateEventsUri(settings);

        // Assert
        Assert.Null(uri);
    }

    [Fact]
    public void CreateEventsUriForLogging_DoesNotContainAriCredentialsOrApiKey()
    {
        // Arrange
        const string secret = "known-ari-secret";
        var settings = new AsteriskResolvedSettings
        {
            BaseUrl = "https://asterisk.test/ari/",
            UserName = "ari-user",
            Password = secret,
            ApplicationName = "contact-center",
        };

        // Act
        var uri = AsteriskSettingsUtilities.CreateEventsUriForLogging(settings);

        // Assert
        Assert.NotNull(uri);
        Assert.DoesNotContain(secret, uri.ToString());
        Assert.DoesNotContain("ari-user", uri.ToString());
        Assert.DoesNotContain("api_key", uri.Query, StringComparison.OrdinalIgnoreCase);
    }
}
