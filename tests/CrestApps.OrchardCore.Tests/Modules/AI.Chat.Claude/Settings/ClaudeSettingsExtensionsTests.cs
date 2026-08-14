using CrestApps.Core.AI.Claude.Models;
using CrestApps.OrchardCore.AI.Chat.Claude.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Claude.Settings;

public sealed class ClaudeSettingsExtensionsTests
{
    [Fact]
    public void ClaudeAuthenticationType_DefaultValue_ShouldBeNotConfigured()
    {
        Assert.Equal(ClaudeAuthenticationType.NotConfigured, default(ClaudeAuthenticationType));
    }

    [Fact]
    public void IsConfigured_WhenAuthenticationTypeIsNotConfigured_ShouldReturnFalse()
    {
        var settings = new ClaudeSettings
        {
            AuthenticationType = ClaudeAuthenticationType.NotConfigured,
        };

        Assert.False(settings.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenApiKeyAuthenticationIsMissingKey_ShouldReturnFalse()
    {
        var settings = new ClaudeSettings
        {
            AuthenticationType = ClaudeAuthenticationType.ApiKey,
        };

        Assert.False(settings.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenApiKeyAuthenticationHasStoredKey_ShouldReturnTrue()
    {
        var settings = new ClaudeSettings
        {
            AuthenticationType = ClaudeAuthenticationType.ApiKey,
            ProtectedApiKey = "protected-api-key",
        };

        Assert.True(settings.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenClaudeOptionsAreMissingApiKey_ShouldReturnFalse()
    {
        var options = new ClaudeOptions();

        Assert.False(options.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenClaudeOptionsHaveApiKey_ShouldReturnTrue()
    {
        var options = new ClaudeOptions
        {
            ApiKey = "api-key",
        };

        Assert.True(options.IsConfigured());
    }
}
