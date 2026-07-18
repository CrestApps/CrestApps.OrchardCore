using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskSettingsUtilitiesTests
{
    [Theory]
    [InlineData("http://asterisk:8088/ari", "ws")]
    [InlineData("https://asterisk:8089/ari", "wss")]
    public void CreateEventsUri_BuildsTenantScopedEventStream(string baseUrl, string expectedScheme)
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
        Assert.Contains("subscribeAll=false", uri.Query);
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
        Assert.Contains("subscribeAll=false", uri.Query);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyDefaults_ResolvedSettings_WhenApplicationNameMissing_LeavesItEmptyToFailClosed(string applicationName)
    {
        // Arrange
        var settings = new AsteriskResolvedSettings
        {
            IsEnabled = true,
            BaseUrl = "https://asterisk.test/ari/",
            UserName = "ari-user",
            Password = "secret",
            ApplicationName = applicationName,
        };

        // Act
        AsteriskSettingsUtilities.ApplyDefaults(settings);

        // Assert
        Assert.True(string.IsNullOrWhiteSpace(settings.ApplicationName));
        Assert.False(AsteriskSettingsUtilities.HasRequiredConfiguration(settings));
    }

    [Fact]
    public void ApplyDefaults_ResolvedSettings_WhenApplicationNameProvided_TrimsWithoutSubstitution()
    {
        // Arrange
        var settings = new AsteriskResolvedSettings
        {
            IsEnabled = true,
            BaseUrl = "https://asterisk.test/ari/",
            UserName = "ari-user",
            Password = "secret",
            ApplicationName = "  tenant-a-contact-center  ",
        };

        // Act
        AsteriskSettingsUtilities.ApplyDefaults(settings);

        // Assert
        Assert.Equal("tenant-a-contact-center", settings.ApplicationName);
        Assert.True(AsteriskSettingsUtilities.HasRequiredConfiguration(settings));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyDefaults_ConnectionSettings_WhenApplicationNameMissing_LeavesItEmptyToFailClosed(string applicationName)
    {
        // Arrange
        var settings = new AsteriskConnectionSettings
        {
            BaseUrl = "https://asterisk.test/ari/",
            UserName = "ari-user",
            ApplicationName = applicationName,
        };

        // Act
        AsteriskSettingsUtilities.ApplyDefaults(settings);

        // Assert
        Assert.True(string.IsNullOrWhiteSpace(settings.ApplicationName));
        Assert.False(AsteriskSettingsUtilities.HasRequiredConfiguration(settings, "secret"));
    }

    [Fact]
    public void CollidesWithHostDefaultApplication_WhenSameApplicationAndServer_ReturnsTrue()
    {
        // Arrange
        // A non-default tenant that explicitly resolves to the host default's application on the same server would
        // cross-deliver Stasis events with the default shell's listener, so the collision must be detected.
        var resolved = new AsteriskResolvedSettings
        {
            BaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl("http://pbx.internal:8088"),
            ApplicationName = "contact-center",
        };
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = "http://pbx.internal:8088",
            ApplicationName = "contact-center",
        };

        // Act
        var collides = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(resolved, defaultOptions);

        // Assert
        Assert.True(collides);
    }

    [Fact]
    public void CollidesWithHostDefaultApplication_WhenApplicationDiffers_ReturnsFalse()
    {
        // Arrange
        // A unique application name is the deployment contract for a shared PBX, so distinct apps never collide.
        var resolved = new AsteriskResolvedSettings
        {
            BaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl("http://pbx.internal:8088"),
            ApplicationName = "tenant-a",
        };
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = "http://pbx.internal:8088",
            ApplicationName = "contact-center",
        };

        // Act
        var collides = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(resolved, defaultOptions);

        // Assert
        Assert.False(collides);
    }

    [Fact]
    public void CollidesWithHostDefaultApplication_WhenServerDiffers_ReturnsFalse()
    {
        // Arrange
        // Distinct PBX servers cannot cross-deliver events even when the application name matches.
        var resolved = new AsteriskResolvedSettings
        {
            BaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl("http://pbx-a.internal:8088"),
            ApplicationName = "contact-center",
        };
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = "http://pbx-b.internal:8088",
            ApplicationName = "contact-center",
        };

        // Act
        var collides = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(resolved, defaultOptions);

        // Assert
        Assert.False(collides);
    }

    [Fact]
    public void CollidesWithHostDefaultApplication_WhenHostDefaultDisabled_ReturnsFalse()
    {
        // Arrange
        // A disabled host default runs no listener, so there is nothing for the tenant to collide with even when the
        // application and server match.
        var resolved = new AsteriskResolvedSettings
        {
            BaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl("http://pbx.internal:8088"),
            ApplicationName = "contact-center",
        };
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = false,
            BaseUrl = "http://pbx.internal:8088",
            ApplicationName = "contact-center",
        };

        // Act
        var collides = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(resolved, defaultOptions);

        // Assert
        Assert.False(collides);
    }

    [Fact]
    public void CollidesWithHostDefaultApplication_WhenResolvedApplicationIsBlank_ReturnsFalse()
    {
        // Arrange
        // A blank application starts no listener (it fails the required-configuration check), so it can never collide.
        var resolved = new AsteriskResolvedSettings
        {
            BaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl("http://pbx.internal:8088"),
            ApplicationName = "   ",
        };
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = "http://pbx.internal:8088",
            ApplicationName = "contact-center",
        };

        // Act
        var collides = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(resolved, defaultOptions);

        // Assert
        Assert.False(collides);
    }

    [Fact]
    public void CollidesWithHostDefaultApplication_WhenServerAndApplicationDifferOnlyByFormatting_ReturnsTrue()
    {
        // Arrange
        // The tenant base URL is normalized at the call site; the host default is normalized inside the check, so a
        // trailing-slash, host-casing, or application-casing difference an operator entered still resolves to the same
        // effective app on the same server and is treated as a collision.
        var resolved = new AsteriskResolvedSettings
        {
            BaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl("http://PBX.internal:8088/ari"),
            ApplicationName = "Contact-Center",
        };
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = "http://pbx.internal:8088",
            ApplicationName = "contact-center",
        };

        // Act
        var collides = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(resolved, defaultOptions);

        // Assert
        Assert.True(collides);
    }

    [Fact]
    public void CollidesWithHostDefaultApplication_StringOverload_AgreesWithResolvedSettingsOverload_WhenColliding()
    {
        // Arrange
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = "http://pbx.internal:8088",
            ApplicationName = "contact-center",
        };
        var rawBaseUrl = "http://pbx.internal:8088/";
        var applicationName = "contact-center";
        var resolvedEquivalent = new AsteriskResolvedSettings
        {
            BaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl(rawBaseUrl),
            ApplicationName = applicationName,
        };

        // Act
        var stringResult = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(rawBaseUrl, applicationName, defaultOptions);
        var resolvedResult = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(resolvedEquivalent, defaultOptions);

        // Assert
        Assert.True(stringResult);
        Assert.Equal(resolvedResult, stringResult);
    }

    [Fact]
    public void CollidesWithHostDefaultApplication_StringOverload_AgreesWithResolvedSettingsOverload_WhenNotColliding()
    {
        // Arrange
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = "http://pbx.internal:8088",
            ApplicationName = "contact-center",
        };
        var rawBaseUrl = "http://pbx.internal:8088/";
        var applicationName = "different-app";
        var resolvedEquivalent = new AsteriskResolvedSettings
        {
            BaseUrl = AsteriskSettingsUtilities.NormalizeBaseUrl(rawBaseUrl),
            ApplicationName = applicationName,
        };

        // Act
        var stringResult = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(rawBaseUrl, applicationName, defaultOptions);
        var resolvedResult = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(resolvedEquivalent, defaultOptions);

        // Assert
        Assert.False(stringResult);
        Assert.Equal(resolvedResult, stringResult);
    }

    [Fact]
    public void CollidesWithHostDefaultApplication_StringOverload_WhenTrailingSlashAndCasingDifference_ReturnsTrue()
    {
        // Arrange
        // Trailing-slash and host-casing differences on an otherwise identical server and application must still
        // be detected as a collision after normalization, the same way the resolved-settings overload detects them.
        var defaultOptions = new DefaultAsteriskOptions
        {
            IsEnabled = true,
            BaseUrl = "http://pbx.internal:8088",
            ApplicationName = "contact-center",
        };

        // Act
        var collides = AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(
            "http://PBX.INTERNAL:8088/ari",
            "Contact-Center",
            defaultOptions);

        // Assert
        Assert.True(collides);
    }
}
