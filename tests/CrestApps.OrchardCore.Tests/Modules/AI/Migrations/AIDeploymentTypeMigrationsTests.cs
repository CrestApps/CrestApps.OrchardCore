using System.Reflection;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIDeploymentTypeMigrationsTests
{
    [Fact]
    public void FindDefaultChatDeploymentName_WhenConnectionNameMatches_ShouldReturnFirstMatchingDeploymentName()
    {
        var profile = CreateProfile("Friendly Connection");
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "embedding-default",
                Name = "embedding-default",
                ClientName = "OpenAI",
                ConnectionName = "Friendly Connection",
            }.Declaring(AIDeploymentFeatureNames.TextEmbedding),
            new AIDeployment
            {
                ItemId = "chat-first",
                Name = "chat-first",
                ClientName = "OpenAI",
                ConnectionName = "Friendly Connection",
            }.Declaring(AIDeploymentFeatureNames.TextGeneration),
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
            }.Declaring(AIDeploymentFeatureNames.TextGeneration),
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
            }.Declaring(AIDeploymentFeatureNames.TextGeneration),
            new AIDeployment
            {
                ItemId = "default-chat",
                Name = "default-chat",
                ClientName = "OpenAI",
                ConnectionName = "default-connection",
            }.Declaring(AIDeploymentFeatureNames.TextGeneration),
            new AIDeployment
            {
                ItemId = "default-utility",
                Name = "default-utility",
                ClientName = "OpenAI",
                ConnectionName = "default-connection",
            }.Declaring(AIDeploymentFeatureNames.TextGeneration),
            new AIDeployment
            {
                ItemId = "default-stt",
                Name = "default-stt",
                ClientName = "OpenAI",
                ConnectionName = "speech-connection",
            }.Declaring(AIDeploymentFeatureNames.SpeechToText),
            new AIDeployment
            {
                ItemId = "default-tts",
                Name = "default-tts",
                ClientName = "OpenAI",
                ConnectionName = "speech-connection",
            }.Declaring(AIDeploymentFeatureNames.TextToSpeech),
        };

        var result = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        Assert.True(result);
        Assert.Equal("default-chat", settings.DefaultChatDeploymentName);

        // The chat and utility purposes both became the one textGeneration capability, so they no longer
        // tell two text deployments apart and the same first candidate fills both defaults. That is
        // harmless: the two slots require the same capability, and the utility slot falls back to the chat
        // one anyway. Speech-to-text and text-to-speech kept capabilities of their own and stay distinct.
        Assert.Equal("default-chat", settings.DefaultUtilityDeploymentName);
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
            }.Declaring(AIDeploymentFeatureNames.TextGeneration),
        };

        var result = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        Assert.True(result);
        Assert.Equal("chat-utility-default", settings.DefaultChatDeploymentName);
        Assert.Equal("chat-utility-default", settings.DefaultUtilityDeploymentName);
    }

    [Fact]
    public void TryCreateDeployment_ShouldPersistConnectionNameInsteadOfItemId()
    {
        var deploymentDoc = new DictionaryDocument<AIDeployment>();
        var connection = CreateConnection(
            itemId: "default-connection-id",
            name: "Default Connection",
            legacyChatDeploymentName: "gpt-4.1-mini");

        var created = InvokeTryCreateDeployment(
            deploymentDoc,
            connection,
            "gpt-4.1-mini",
            LegacyDeploymentPurposes.Chat);

        Assert.True(created);
        var deployment = Assert.Single(deploymentDoc.Records.Values);
        Assert.Equal("Default Connection", deployment.ConnectionName);
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
            }.Declaring(AIDeploymentFeatureNames.TextGeneration),
            new AIDeployment
            {
                ItemId = "global-default-embedding",
                Name = "global-default-embedding",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
            }.Declaring(AIDeploymentFeatureNames.TextEmbedding),
            new AIDeployment
            {
                ItemId = "global-default-image",
                Name = "global-default-image",
                ClientName = "OpenAI",
                ConnectionName = "legacy-connection",
            }.Declaring(AIDeploymentFeatureNames.ImageOutput),
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

    private static bool InvokeTryCreateDeployment(
        DictionaryDocument<AIDeployment> deploymentDoc,
        AIProviderConnection connection,
        string deploymentName,
        int type)
    {
        var assembly = Assembly.Load("CrestApps.OrchardCore.AI");
        var migrationType = assembly.GetType(
            "CrestApps.OrchardCore.AI.Migrations.AIDeploymentTypeMigrations",
            throwOnError: true);
        var method = migrationType.GetMethod(
            "TryCreateDeployment",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(DictionaryDocument<AIDeployment>), typeof(AIProviderConnection), typeof(string), LegacyDeploymentPurposes.PurposeType],
            modifiers: null);

        return (bool)method.Invoke(null, [deploymentDoc, connection, deploymentName, type])!;
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
        var connection = new AIProviderConnection
        {
            ItemId = itemId,
            Name = name,
            ClientName = "OpenAI",
        };

        connection.SetLegacyChatDeploymentName(legacyChatDeploymentName);
        connection.SetLegacyUtilityDeploymentName(legacyUtilityDeploymentName);
        connection.SetLegacyEmbeddingDeploymentName(legacyEmbeddingDeploymentName);

        return connection;
    }
}
