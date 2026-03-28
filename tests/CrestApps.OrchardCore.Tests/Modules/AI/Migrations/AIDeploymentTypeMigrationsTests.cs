using System.Reflection;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIDeploymentTypeMigrationsTests
{
    [Fact]
    public void FindDefaultChatDeploymentId_WhenDefaultDeploymentExists_ShouldReturnDefaultDeploymentId()
    {
        var profile = CreateProfile("legacy-connection");
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "chat-secondary",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat,
            },
            new AIDeployment
            {
                ItemId = "chat-default",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat,
                IsDefault = true,
            },
        };

        var result = InvokeFindDefaultChatDeploymentId(profile, deployments);

        Assert.Equal("chat-default", result);
    }

    [Fact]
    public void FindDefaultChatDeploymentId_WhenConnectionAliasMatches_ShouldReturnFirstMatchingDeploymentId()
    {
        var profile = CreateProfile("Friendly Connection");
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "embedding-default",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                ConnectionNameAlias = "Friendly Connection",
                Type = AIDeploymentType.Embedding,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "chat-first",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                ConnectionNameAlias = "Friendly Connection",
                Type = AIDeploymentType.Chat,
            },
        };

        var result = InvokeFindDefaultChatDeploymentId(profile, deployments);

        Assert.Equal("chat-first", result);
    }

    [Fact]
    public void FindDefaultChatDeploymentId_WhenMultiTypeDeploymentMatches_ShouldReturnDeploymentId()
    {
        var profile = CreateProfile("legacy-connection");
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "chat-utility-default",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat | AIDeploymentType.Utility,
                IsDefault = true,
            },
        };

        var result = InvokeFindDefaultChatDeploymentId(profile, deployments);

        Assert.Equal("chat-utility-default", result);
    }

    [Fact]
    public void TryPopulateDefaultDeploymentSettings_WhenSettingsAreNull_ShouldBackfillAvailableDeploymentTypes()
    {
        var settings = new DefaultAIDeploymentSettings();
        var connections = new[]
        {
            CreateConnection(itemId: "secondary-connection", name: "Secondary", legacyChatDeploymentName: "gpt-4.1"),
            CreateConnection(itemId: "default-connection", name: "Default", legacyChatDeploymentName: "gpt-4o-mini", legacyUtilityDeploymentName: "gpt-4o-mini", isDefault: true),
        };
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "secondary-chat",
                ClientName = "OpenAI",
                ConnectionName = "secondary-connection",
                ConnectionNameAlias = "Secondary",
                Type = AIDeploymentType.Chat,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "default-chat",
                ClientName = "OpenAI",
                ConnectionName = "default-connection",
                ConnectionNameAlias = "Default",
                Type = AIDeploymentType.Chat,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "default-utility",
                ClientName = "OpenAI",
                ConnectionName = "default-connection",
                ConnectionNameAlias = "Default",
                Type = AIDeploymentType.Utility,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "default-stt",
                ClientName = "OpenAI",
                ConnectionName = "speech-connection",
                Type = AIDeploymentType.SpeechToText,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "default-tts",
                ClientName = "OpenAI",
                ConnectionName = "speech-connection",
                Type = AIDeploymentType.TextToSpeech,
                IsDefault = true,
            },
        };

        var result = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        Assert.True(result);
        Assert.Equal("default-chat", settings.DefaultChatDeploymentId);
        Assert.Equal("default-utility", settings.DefaultUtilityDeploymentId);
        Assert.Equal("default-stt", settings.DefaultSpeechToTextDeploymentId);
        Assert.Equal("default-tts", settings.DefaultTextToSpeechDeploymentId);
    }

    [Fact]
    public void TryPopulateDefaultDeploymentSettings_WhenDeploymentSupportsMultipleTypes_ShouldReuseSameDeploymentId()
    {
        var settings = new DefaultAIDeploymentSettings();
        var connections = new[]
        {
            CreateConnection(itemId: "default-connection", name: "Default", legacyChatDeploymentName: "gpt-4.1-mini", legacyUtilityDeploymentName: "gpt-4.1-mini", isDefault: true),
        };
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "chat-utility-default",
                ClientName = "OpenAI",
                ConnectionName = "default-connection",
                ConnectionNameAlias = "Default",
                Type = AIDeploymentType.Chat | AIDeploymentType.Utility,
                IsDefault = true,
            },
        };

        var result = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        Assert.True(result);
        Assert.Equal("chat-utility-default", settings.DefaultChatDeploymentId);
        Assert.Equal("chat-utility-default", settings.DefaultUtilityDeploymentId);
    }

    [Fact]
    public void TryPopulateDefaultDeploymentSettings_WhenSettingsAlreadyExist_ShouldNotOverwriteThem()
    {
        var settings = new DefaultAIDeploymentSettings
        {
            DefaultChatDeploymentId = "existing-chat",
            DefaultEmbeddingDeploymentId = "existing-embedding",
        };
        var connections = new[]
        {
            CreateConnection(itemId: "legacy-connection", name: "Legacy", legacyChatDeploymentName: "gpt-4o-mini", legacyEmbeddingDeploymentName: "text-embedding-3-small"),
        };
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "global-default-chat",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "global-default-embedding",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Embedding,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "global-default-image",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Image,
                IsDefault = true,
            },
        };

        var result = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        Assert.True(result);
        Assert.Equal("existing-chat", settings.DefaultChatDeploymentId);
        Assert.Equal("existing-embedding", settings.DefaultEmbeddingDeploymentId);
        Assert.Equal("global-default-image", settings.DefaultImageDeploymentId);
    }

    private static string InvokeFindDefaultChatDeploymentId(AIProfile profile, IEnumerable<AIDeployment> deployments)
    {
        var assembly = Assembly.Load("CrestApps.OrchardCore.AI");
        var type = assembly.GetType(
            "CrestApps.OrchardCore.AI.Migrations.AIDeploymentTypeMigrations",
            throwOnError: true)!;
        var method = type.GetMethod("FindDefaultChatDeploymentId", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (string)method.Invoke(null, [profile, deployments])!;
    }

    private static bool InvokeTryPopulateDefaultDeploymentSettings(
        DefaultAIDeploymentSettings settings,
        IEnumerable<AIProviderConnection> connections,
        IEnumerable<AIDeployment> deployments)
    {
        var assembly = Assembly.Load("CrestApps.OrchardCore.AI");
        var type = assembly.GetType(
            "CrestApps.OrchardCore.AI.Migrations.AIDeploymentTypeMigrations",
            throwOnError: true);
        var method = type.GetMethod(
            "TryPopulateDefaultDeploymentSettings",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(DefaultAIDeploymentSettings), typeof(IEnumerable<AIProviderConnection>), typeof(IEnumerable<AIDeployment>)],
            modifiers: null);

        return (bool)method.Invoke(null, [settings, connections, deployments])!;
    }

    private static AIProfile CreateProfile(string connectionName)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return new AIProfile
        {
            ConnectionName = connectionName,
        };
#pragma warning restore CS0618 // Type or member is obsolete
    }

    private static AIProviderConnection CreateConnection(
        string itemId,
        string name,
        string legacyChatDeploymentName,
        string legacyUtilityDeploymentName = null,
        string legacyEmbeddingDeploymentName = null,
        bool isDefault = false)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return new AIProviderConnection
        {
            ItemId = itemId,
            Name = name,
            ClientName = "OpenAI",
            ChatDeploymentName = legacyChatDeploymentName,
            UtilityDeploymentName = legacyUtilityDeploymentName,
            EmbeddingDeploymentName = legacyEmbeddingDeploymentName,
            IsDefault = isDefault,
        };
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
