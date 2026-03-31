using System.Reflection;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIDeploymentTypeMigrationsTests
{
    [Fact]
    public void FindDefaultChatDeploymentName_WhenDefaultDeploymentExists_ShouldReturnDefaultDeploymentName()
    {
        var profile = CreateProfile("legacy-connection");
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "chat-secondary",
                Name = "chat-secondary",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat,
            },
            new AIDeployment
            {
                ItemId = "chat-default",
                Name = "chat-default",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat,
                IsDefault = true,
            },
        };

        var result = InvokeFindDefaultChatDeploymentName(profile, deployments);

        Assert.Equal("chat-default", result);
    }

    [Fact]
    public void FindDefaultChatDeploymentName_WhenConnectionAliasMatches_ShouldReturnFirstMatchingDeploymentName()
    {
        var profile = CreateProfile("Friendly Connection");
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "embedding-default",
                Name = "embedding-default",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                ConnectionNameAlias = "Friendly Connection",
                Type = AIDeploymentType.Embedding,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "chat-first",
                Name = "chat-first",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                ConnectionNameAlias = "Friendly Connection",
                Type = AIDeploymentType.Chat,
            },
        };

        var result = InvokeFindDefaultChatDeploymentName(profile, deployments);

        Assert.Equal("chat-first", result);
    }

    [Fact]
    public void FindDefaultChatDeploymentName_WhenMultiTypeDeploymentMatches_ShouldReturnDeploymentName()
    {
        var profile = CreateProfile("legacy-connection");
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "chat-utility-default",
                Name = "chat-utility-default",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat | AIDeploymentType.Utility,
                IsDefault = true,
            },
        };

        var result = InvokeFindDefaultChatDeploymentName(profile, deployments);

        Assert.Equal("chat-utility-default", result);
    }

    [Fact]
    public void TryPopulateDefaultDeploymentSettings_WhenSettingsAreNull_ShouldBackfillAvailableDeploymentTypes()
    {
        var settings = new DefaultAIDeploymentSettings();
        var connections = new[]
        {
            CreateConnection(itemId: "secondary-connection", name: "Secondary", legacyChatDeploymentName: "gpt-4.1"),
            CreateConnection(itemId: "default-connection", name: "Default", legacyChatDeploymentName: "gpt-4o-mini", legacyUtilityDeploymentName: "gpt-4o-mini"),
        };
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "secondary-chat",
                Name = "secondary-chat",
                ClientName = "OpenAI",
                ConnectionName = "secondary-connection",
                ConnectionNameAlias = "Secondary",
                Type = AIDeploymentType.Chat,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "default-chat",
                Name = "default-chat",
                ClientName = "OpenAI",
                ConnectionName = "default-connection",
                ConnectionNameAlias = "Default",
                Type = AIDeploymentType.Chat,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "default-utility",
                Name = "default-utility",
                ClientName = "OpenAI",
                ConnectionName = "default-connection",
                ConnectionNameAlias = "Default",
                Type = AIDeploymentType.Utility,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "default-stt",
                Name = "default-stt",
                ClientName = "OpenAI",
                ConnectionName = "speech-connection",
                Type = AIDeploymentType.SpeechToText,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "default-tts",
                Name = "default-tts",
                ClientName = "OpenAI",
                ConnectionName = "speech-connection",
                Type = AIDeploymentType.TextToSpeech,
                IsDefault = true,
            },
        };

        var result = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        Assert.True(result);
        Assert.Equal("default-chat", settings.DefaultChatDeploymentName);
        Assert.Equal("default-utility", settings.DefaultUtilityDeploymentName);
        Assert.Equal("default-stt", settings.DefaultSpeechToTextDeploymentName);
        Assert.Equal("default-tts", settings.DefaultTextToSpeechDeploymentName);
    }

    [Fact]
    public void TryPopulateDefaultDeploymentSettings_WhenDeploymentSupportsMultipleTypes_ShouldReuseSameDeploymentName()
    {
        var settings = new DefaultAIDeploymentSettings();
        var connections = new[]
        {
            CreateConnection(itemId: "default-connection", name: "Default", legacyChatDeploymentName: "gpt-4.1-mini", legacyUtilityDeploymentName: "gpt-4.1-mini"),
        };
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "chat-utility-default",
                Name = "chat-utility-default",
                ClientName = "OpenAI",
                ConnectionName = "default-connection",
                ConnectionNameAlias = "Default",
                Type = AIDeploymentType.Chat | AIDeploymentType.Utility,
                IsDefault = true,
            },
        };

        var result = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        Assert.True(result);
        Assert.Equal("chat-utility-default", settings.DefaultChatDeploymentName);
        Assert.Equal("chat-utility-default", settings.DefaultUtilityDeploymentName);
    }

    [Fact]
    public void TryPopulateDefaultDeploymentSettings_WhenSettingsAlreadyExist_ShouldNotOverwriteThem()
    {
        var settings = new DefaultAIDeploymentSettings
        {
            DefaultChatDeploymentName = "existing-chat",
            DefaultEmbeddingDeploymentName = "existing-embedding",
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
                Name = "global-default-chat",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "global-default-embedding",
                Name = "global-default-embedding",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Embedding,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "global-default-image",
                Name = "global-default-image",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Image,
                IsDefault = true,
            },
        };

        var result = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        Assert.True(result);
        Assert.Equal("existing-chat", settings.DefaultChatDeploymentName);
        Assert.Equal("existing-embedding", settings.DefaultEmbeddingDeploymentName);
        Assert.Equal("global-default-image", settings.DefaultImageDeploymentName);
    }

    [Fact]
    public void TryConvertDeploymentSelectorToName_WhenDefaultSettingsContainDeploymentIds_ShouldConvertToNames()
    {
        var settings = new DefaultAIDeploymentSettings
        {
            DefaultChatDeploymentName = "chat-id",
            DefaultUtilityDeploymentName = "utility-id",
            DefaultEmbeddingDeploymentName = "embedding-name",
        };

        var deploymentNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["chat-id"] = "chat-name",
            ["utility-id"] = "utility-name",
        };

        var chatUpdated = InvokeTryConvertDeploymentSelectorToName(
            deploymentNameMap,
            settings.DefaultChatDeploymentName,
            value => settings.DefaultChatDeploymentName = value);
        var utilityUpdated = InvokeTryConvertDeploymentSelectorToName(
            deploymentNameMap,
            settings.DefaultUtilityDeploymentName,
            value => settings.DefaultUtilityDeploymentName = value);
        var embeddingUpdated = InvokeTryConvertDeploymentSelectorToName(
            deploymentNameMap,
            settings.DefaultEmbeddingDeploymentName,
            value => settings.DefaultEmbeddingDeploymentName = value);

        Assert.True(chatUpdated);
        Assert.True(utilityUpdated);
        Assert.False(embeddingUpdated);
        Assert.Equal("chat-name", settings.DefaultChatDeploymentName);
        Assert.Equal("utility-name", settings.DefaultUtilityDeploymentName);
        Assert.Equal("embedding-name", settings.DefaultEmbeddingDeploymentName);
    }

    private static string InvokeFindDefaultChatDeploymentName(AIProfile profile, IEnumerable<AIDeployment> deployments)
    {
        var assembly = Assembly.Load("CrestApps.OrchardCore.AI");
        var type = assembly.GetType(
            "CrestApps.OrchardCore.AI.Migrations.AIDeploymentTypeMigrations",
            throwOnError: true)!;
        var method = type.GetMethod("FindDefaultChatDeploymentName", BindingFlags.NonPublic | BindingFlags.Static)!;

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

    private static bool InvokeTryConvertDeploymentSelectorToName(
        IReadOnlyDictionary<string, string> deploymentNameMap,
        string currentValue,
        Action<string> assign)
    {
        var assembly = Assembly.Load("CrestApps.OrchardCore.AI");
        var type = assembly.GetType(
            "CrestApps.OrchardCore.AI.Migrations.AIDeploymentTypeMigrations",
            throwOnError: true);
        var method = type.GetMethod(
            "TryConvertDeploymentSelectorToName",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(IReadOnlyDictionary<string, string>), typeof(string), typeof(Action<string>)],
            modifiers: null);

        return (bool)method.Invoke(null, [deploymentNameMap, currentValue, assign])!;
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
        string legacyEmbeddingDeploymentName = null)
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
        };
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
