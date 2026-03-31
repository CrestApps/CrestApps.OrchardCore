using CrestApps.OrchardCore.AI.Chat.Copilot.Models;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Copilot.Settings;

public sealed class CopilotSettingsExtensionsTests
{
    [Fact]
    public void CopilotAuthenticationType_DefaultValue_ShouldBeNotConfigured()
    {
        Assert.Equal(CopilotAuthenticationType.NotConfigured, default(CopilotAuthenticationType));
    }

    [Fact]
    public void IsConfigured_WhenAuthenticationTypeIsNotConfigured_ShouldReturnFalse()
    {
        var settings = new CopilotSettings
        {
            AuthenticationType = CopilotAuthenticationType.NotConfigured,
        };

        Assert.False(settings.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenGitHubOAuthIsMissingSecret_ShouldReturnFalse()
    {
        var settings = new CopilotSettings
        {
            AuthenticationType = CopilotAuthenticationType.GitHubOAuth,
            ClientId = "client-id",
        };

        Assert.False(settings.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenGitHubOAuthHasClientIdAndSecret_ShouldReturnTrue()
    {
        var settings = new CopilotSettings
        {
            AuthenticationType = CopilotAuthenticationType.GitHubOAuth,
            ClientId = "client-id",
            ProtectedClientSecret = "protected-secret",
        };

        Assert.True(settings.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenAzureByokIsMissingApiVersion_ShouldReturnFalse()
    {
        var settings = new CopilotSettings
        {
            AuthenticationType = CopilotAuthenticationType.ApiKey,
            ProviderType = "azure",
            BaseUrl = "https://example.openai.azure.com",
            ProtectedApiKey = "protected-api-key",
            DefaultModel = "gpt-4.1",
        };

        Assert.False(settings.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenOpenAiCompatibleByokHasRequiredSettings_ShouldReturnTrue()
    {
        var settings = new CopilotSettings
        {
            AuthenticationType = CopilotAuthenticationType.ApiKey,
            ProviderType = "openai",
            BaseUrl = "https://example.test/v1",
            DefaultModel = "gpt-4.1-mini",
        };

        Assert.True(settings.IsConfigured());
    }
}
