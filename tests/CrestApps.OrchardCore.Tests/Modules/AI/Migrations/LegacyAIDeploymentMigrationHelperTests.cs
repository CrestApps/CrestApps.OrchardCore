using System.Reflection;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class LegacyAIDeploymentMigrationHelperTests
{
    [Fact]
    public void FindWritableDeployment_WhenWritableMigratedAliasExists_ShouldMatchByModelName()
    {
        // Arrange
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "cfg-id",
                Name = "gpt-4.1-mini",
                ModelName = "gpt-4.1-mini",
                Source = "Azure",
                ConnectionName = "winnerware",
                IsReadOnly = true,
                Type = AIDeploymentType.Utility,
            },
            new AIDeployment
            {
                ItemId = "db-id",
                Name = "gpt-4.1-mini-migrated",
                ModelName = "gpt-4.1-mini",
                Source = "Azure",
                ConnectionName = "winnerware",
                Type = AIDeploymentType.Chat,
            },
        };

        // Act
        var deployment = InvokeFindWritableDeployment(
            deployments,
            itemId: "legacy-utility-id",
            deploymentName: "gpt-4.1-mini",
            modelName: "gpt-4.1-mini",
            sourceName: "Azure",
            connectionName: "winnerware");

        // Assert
        Assert.NotNull(deployment);
        Assert.Equal("db-id", deployment.ItemId);
    }

    [Fact]
    public void GenerateUniqueDeploymentName_WhenBaseAndMigratedNamesExist_ShouldIncrementSuffix()
    {
        // Arrange
        var deployments = new[]
        {
            new AIDeployment { ItemId = "1", Name = "gpt-4.1-mini" },
            new AIDeployment { ItemId = "2", Name = "gpt-4.1-mini-migrated" },
        };

        // Act
        var uniqueName = InvokeGenerateUniqueDeploymentName(deployments, "gpt-4.1-mini");

        // Assert
        Assert.Equal("gpt-4.1-mini-migrated-2", uniqueName);
    }

    [Theory]
    [InlineData(AIDeploymentType.Chat, AIDeploymentType.Chat | AIDeploymentType.Utility)]
    [InlineData(AIDeploymentType.Utility, AIDeploymentType.Chat | AIDeploymentType.Utility)]
    [InlineData(AIDeploymentType.Chat | AIDeploymentType.Utility, AIDeploymentType.Chat | AIDeploymentType.Utility)]
    [InlineData(AIDeploymentType.Embedding, AIDeploymentType.Embedding)]
    public void NormalizeInteractiveTypes_ShouldMirrorChatAndUtilitySupport(
        AIDeploymentType input,
        AIDeploymentType expected)
    {
        // Act
        var deploymentType = InvokeNormalizeInteractiveTypes(input);

        // Assert
        Assert.Equal(expected, deploymentType);
    }

    [Fact]
    public void TryPopulateDefaultDeploymentSettings_WhenMatchingDeploymentsExist_ShouldBackfillAllMissingDefaults()
    {
        // Arrange
        var settings = new DefaultAIDeploymentSettings();
        var connections = new[]
        {
            new AIProviderConnection
            {
                ItemId = "winnerware-id",
                Name = "winnerware",
                ClientName = "Azure",
            },
        };
        connections[0].SetLegacyChatDeploymentName("gpt-4.1-mini");
        connections[0].SetLegacyUtilityDeploymentName("gpt-4.1-mini");
        connections[0].SetLegacyEmbeddingDeploymentName("text-embedding-3-small");

        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "migrated-id",
                Name = "gpt-4.1-mini-migrated",
                ModelName = "gpt-4.1-mini",
                Source = "Azure",
                ConnectionName = "winnerware",
                Type = AIDeploymentType.Chat | AIDeploymentType.Utility,
            },
            new AIDeployment
            {
                ItemId = "embedding-id",
                Name = "text-embedding-3-small-migrated",
                ModelName = "text-embedding-3-small",
                Source = "Azure",
                ConnectionName = "winnerware",
                Type = AIDeploymentType.Embedding,
            },
            new AIDeployment
            {
                ItemId = "stt-id",
                Name = "whisper",
                ModelName = "whisper",
                Source = "Azure",
                ConnectionName = "winnerware",
                Type = AIDeploymentType.SpeechToText,
            },
            new AIDeployment
            {
                ItemId = "tts-id",
                Name = "AzureTextToSpeech",
                ModelName = "AzureTextToSpeech",
                Source = "Azure",
                ConnectionName = "winnerware",
                Type = AIDeploymentType.TextToSpeech,
            },
        };

        // Act
        var updated = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        // Assert
        Assert.True(updated);
        Assert.Equal("gpt-4.1-mini-migrated", settings.DefaultChatDeploymentName);
        Assert.Equal("gpt-4.1-mini-migrated", settings.DefaultUtilityDeploymentName);
        Assert.Equal("text-embedding-3-small-migrated", settings.DefaultEmbeddingDeploymentName);
        Assert.Equal("whisper", settings.DefaultSpeechToTextDeploymentName);
        Assert.Equal("AzureTextToSpeech", settings.DefaultTextToSpeechDeploymentName);
    }

    private static AIDeployment InvokeFindWritableDeployment(
        IEnumerable<AIDeployment> deployments,
        string itemId,
        string deploymentName,
        string modelName,
        string sourceName,
        string connectionName)
    {
        var method = GetHelperType().GetMethod(
            "FindWritableDeployment",
            BindingFlags.Public | BindingFlags.Static)!;

        return (AIDeployment)method.Invoke(null, [deployments, itemId, deploymentName, modelName, sourceName, connectionName])!;
    }

    private static string InvokeGenerateUniqueDeploymentName(IEnumerable<AIDeployment> deployments, string deploymentName)
    {
        var method = GetHelperType().GetMethod(
            "GenerateUniqueDeploymentName",
            BindingFlags.Public | BindingFlags.Static)!;

        return (string)method.Invoke(null, [deployments, deploymentName])!;
    }

    private static AIDeploymentType InvokeNormalizeInteractiveTypes(AIDeploymentType deploymentType)
    {
        var method = GetHelperType().GetMethod(
            "NormalizeInteractiveTypes",
            BindingFlags.Public | BindingFlags.Static)!;

        return (AIDeploymentType)method.Invoke(null, [deploymentType])!;
    }

    private static bool InvokeTryPopulateDefaultDeploymentSettings(
        DefaultAIDeploymentSettings settings,
        IEnumerable<AIProviderConnection> connections,
        IEnumerable<AIDeployment> deployments)
    {
        var method = GetHelperType().GetMethod(
            "TryPopulateDefaultDeploymentSettings",
            BindingFlags.Public | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [settings, connections, deployments])!;
    }

    private static Type GetHelperType()
        => typeof(Startup).Assembly.GetType(
            "CrestApps.OrchardCore.AI.Migrations.LegacyAIDeploymentMigrationHelper",
            throwOnError: true)!;
}
