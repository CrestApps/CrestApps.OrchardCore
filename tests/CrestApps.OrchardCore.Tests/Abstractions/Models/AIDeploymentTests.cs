using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Abstractions.Models;

public sealed class AIDeploymentTests
{
    [Fact]
    public void ModelName_WhenUnset_ShouldFallbackToTechnicalName()
    {
        var deployment = new AIDeployment
        {
            Name = "openai-chat",
        };

        Assert.Equal("openai-chat", deployment.ModelName);
    }

    [Fact]
    public void Clone_WhenModelNameIsUnset_ShouldPreserveLegacyFallbackBehavior()
    {
        var deployment = new AIDeployment
        {
            ItemId = "dep-1",
            Name = "azure-chat",
        };

        var clone = deployment.Clone();

        Assert.Equal("azure-chat", clone.Name);
        Assert.Equal("azure-chat", clone.ModelName);
    }

    [Fact]
    public void Deserialize_WhenModelNameIsPresent_ShouldPreserveStoredModelName()
    {
        var deployment = JsonSerializer.Deserialize<AIDeployment>(
            """
            {
              "Name": "openai-chat",
              "ModelName": "gpt-4.1"
            }
            """);

        Assert.NotNull(deployment);
        Assert.Equal("openai-chat", deployment.Name);
        Assert.Equal("gpt-4.1", deployment.ModelName);
    }

    [Fact]
    public void Deserialize_DefaultSettings_WhenLegacyDeploymentIdsArePresent_ShouldPopulateDeploymentNames()
    {
        var settings = JsonSerializer.Deserialize<DefaultAIDeploymentSettings>(
            """
            {
              "DefaultChatDeploymentId": "chat-technical-name",
              "DefaultUtilityDeploymentId": "utility-technical-name",
              "DefaultEmbeddingDeploymentId": "embedding-technical-name"
            }
            """);

        Assert.NotNull(settings);
        Assert.Equal("chat-technical-name", settings.DefaultChatDeploymentName);
        Assert.Equal("utility-technical-name", settings.DefaultUtilityDeploymentName);
        Assert.Equal("embedding-technical-name", settings.DefaultEmbeddingDeploymentName);
    }

    [Fact]
    public void Deserialize_Profile_WhenLegacyDeploymentIdsArePresent_ShouldPopulateDeploymentNames()
    {
        var profile = JsonSerializer.Deserialize<AIProfile>(
            """
            {
              "ChatDeploymentId": "chat-technical-name",
              "UtilityDeploymentId": "utility-technical-name"
            }
            """);

        Assert.NotNull(profile);
        Assert.Equal("chat-technical-name", profile.ChatDeploymentName);
        Assert.Equal("utility-technical-name", profile.UtilityDeploymentName);
    }

    [Fact]
    public void Deserialize_ChatInteraction_WhenLegacyDeploymentIdsArePresent_ShouldPopulateDeploymentNames()
    {
        var interaction = JsonSerializer.Deserialize<ChatInteraction>(
            """
            {
              "DeploymentId": "chat-technical-name",
              "UtilityDeploymentId": "utility-technical-name"
            }
            """);

        Assert.NotNull(interaction);
        Assert.Equal("chat-technical-name", interaction.ChatDeploymentName);
        Assert.Equal("utility-technical-name", interaction.UtilityDeploymentName);
    }
}
