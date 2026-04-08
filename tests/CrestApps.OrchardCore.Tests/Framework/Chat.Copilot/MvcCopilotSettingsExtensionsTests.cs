using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Models;
using CrestApps.Core.Mvc.Web.Areas.AIChat.Services;

namespace CrestApps.OrchardCore.Tests.Framework.Chat.Copilot;

public sealed class MvcCopilotSettingsExtensionsTests
{
    [Fact]
    public void IsConfigured_WhenSettingsAuthenticationTypeIsNotConfigured_ShouldReturnFalse()
    {
        var settings = new CopilotSettings
        {
            AuthenticationType = CopilotAuthenticationType.NotConfigured,
        };

        Assert.False(settings.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenSettingsGitHubOAuthHasClientIdAndSecret_ShouldReturnTrue()
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
    public void IsConfigured_WhenSettingsAzureByokIsMissingApiVersion_ShouldReturnFalse()
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
    public void IsConfigured_WhenSettingsOpenAiCompatibleByokHasRequiredSettings_ShouldReturnTrue()
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

    [Fact]
    public void IsConfigured_WhenOptionsAuthenticationTypeIsNotConfigured_ShouldReturnFalse()
    {
        var options = new CopilotOptions
        {
            AuthenticationType = CopilotAuthenticationType.NotConfigured,
        };

        Assert.False(options.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenOptionsAzureByokIsMissingApiKey_ShouldReturnFalse()
    {
        var options = new CopilotOptions
        {
            AuthenticationType = CopilotAuthenticationType.ApiKey,
            ProviderType = "azure",
            BaseUrl = "https://example.openai.azure.com",
            DefaultModel = "gpt-4.1",
            AzureApiVersion = "2024-12-01-preview",
        };

        Assert.False(options.IsConfigured());
    }

    [Fact]
    public void IsConfigured_WhenOptionsOpenAiCompatibleByokHasRequiredSettings_ShouldReturnTrue()
    {
        var options = new CopilotOptions
        {
            AuthenticationType = CopilotAuthenticationType.ApiKey,
            ProviderType = "openai",
            BaseUrl = "https://example.test/v1",
            DefaultModel = "gpt-4.1-mini",
        };

        Assert.True(options.IsConfigured());
    }
}
