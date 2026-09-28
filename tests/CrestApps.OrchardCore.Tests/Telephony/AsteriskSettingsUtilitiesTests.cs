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

    [Theory]
    [InlineData("crestapps-telephony", "TenantA", "crestapps-telephony-TenantA")]
    [InlineData("crestapps-telephony", "Default", "crestapps-telephony-Default")]
    [InlineData("  crestapps-telephony  ", "  TenantA  ", "crestapps-telephony-TenantA")]
    public void BuildHostDefaultApplicationName_WhenApplicationAndShellProvided_SuffixesWithShellName(
        string applicationName,
        string shellName,
        string expected)
    {
        // Act
        var result = AsteriskSettingsUtilities.BuildHostDefaultApplicationName(applicationName, shellName);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildHostDefaultApplicationName_WhenShellNameMissing_ReturnsTrimmedApplicationName(string shellName)
    {
        // Act
        var result = AsteriskSettingsUtilities.BuildHostDefaultApplicationName("  crestapps-telephony  ", shellName);

        // Assert
        Assert.Equal("crestapps-telephony", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildHostDefaultApplicationName_WhenApplicationNameMissing_ReturnsApplicationNameUnsuffixed(string applicationName)
    {
        // Act
        var result = AsteriskSettingsUtilities.BuildHostDefaultApplicationName(applicationName, "TenantA");

        // Assert
        Assert.True(string.IsNullOrWhiteSpace(result));
    }
}
