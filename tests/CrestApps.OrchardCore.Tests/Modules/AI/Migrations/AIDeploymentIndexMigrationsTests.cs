using System.Collections;
using System.Reflection;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIDeploymentIndexMigrationsTests
{
    [Fact]
    public void InferLegacyDeploymentType_WhenProfileReferencesLegacyDeploymentId_ShouldReturnChat()
    {
        // Arrange
        var profileDeploymentTypesById = CreateDeploymentTypeHints(("legacy-chat-id", LegacyDeploymentPurposes.Chat));

        // Act
        var deploymentType = InvokeInferLegacyDeploymentType(
            itemId: "legacy-chat-id",
            deploymentName: "gpt-4.1-mini",
            connectionSelector: "winnerware",
            sourceName: "Azure",
            profileDeploymentTypesById,
            CreateDeploymentTypeHints(),
            []);

        // Assert
        Assert.Equal(LegacyDeploymentPurposes.Chat, deploymentType);
    }

    [Fact]
    public void InferLegacyDeploymentType_WhenConnectionReferencesEmbeddingDeployment_ShouldReturnEmbedding()
    {
        // Arrange
        var connections = new[]
        {
            CreateConnection(
                itemId: "winnerware-id",
                name: "winnerware",
                clientName: "Azure",
                legacyEmbeddingDeploymentName: "text-embedding-3-small"),
        };

        // Act
        var deploymentType = InvokeInferLegacyDeploymentType(
            itemId: "embedding-id",
            deploymentName: "text-embedding-3-small",
            connectionSelector: "winnerware",
            sourceName: "Azure",
            CreateDeploymentTypeHints(),
            CreateDeploymentTypeHints(),
            connections);

        // Assert
        Assert.Equal(LegacyDeploymentPurposes.Embedding, deploymentType);
    }

    [Fact]
    public void InferLegacyDeploymentType_WhenConnectionReferencesUtilityDeployment_ShouldReturnChatAndUtility()
    {
        // Arrange
        var connections = new[]
        {
            CreateConnection(
                itemId: "winnerware-id",
                name: "winnerware",
                clientName: "Azure",
                legacyUtilityDeploymentName: "gpt-4.1-mini"),
        };

        // Act
        var deploymentType = InvokeInferLegacyDeploymentType(
            itemId: "utility-id",
            deploymentName: "gpt-4.1-mini",
            connectionSelector: "winnerware",
            sourceName: "Azure",
            CreateDeploymentTypeHints(),
            CreateDeploymentTypeHints(),
            connections);

        // Assert
        Assert.Equal(LegacyDeploymentPurposes.Chat | LegacyDeploymentPurposes.Utility, deploymentType);
    }

    [Fact]
    public void InferLegacyDeploymentType_WhenNoLegacyHintsExist_ShouldFallbackToChat()
    {
        // Arrange

        // Act
        var deploymentType = InvokeInferLegacyDeploymentType(
            itemId: "legacy-id",
            deploymentName: "gpt-4.1-mini",
            connectionSelector: "winnerware",
            sourceName: "Azure",
            CreateDeploymentTypeHints(),
            CreateDeploymentTypeHints(),
            []);

        // Assert
        Assert.Equal(LegacyDeploymentPurposes.Chat, deploymentType);
    }

    /// <summary>
    /// Builds a hint dictionary keyed to the migrations' internal legacy purpose enum, which the reflected
    /// method's signature requires exactly.
    /// </summary>
    private static object CreateDeploymentTypeHints(params (string Key, int Purpose)[] hints)
    {
        var dictionary = (IDictionary)Activator.CreateInstance(
            typeof(Dictionary<,>).MakeGenericType(typeof(string), LegacyDeploymentPurposes.PurposeType),
            [StringComparer.OrdinalIgnoreCase])!;

        foreach (var (key, purpose) in hints)
        {
            dictionary[key] = LegacyDeploymentPurposes.Box(purpose);
        }

        return dictionary;
    }

    private static int InvokeInferLegacyDeploymentType(
        string itemId,
        string deploymentName,
        string connectionSelector,
        string sourceName,
        object profileDeploymentTypesById,
        object profileDeploymentTypesByName,
        IEnumerable<AIProviderConnection> legacyConnections)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.AIDeploymentIndexMigrations", throwOnError: true)!
            .GetMethod(
                "InferLegacyDeploymentType",
                BindingFlags.NonPublic | BindingFlags.Static)!;

        return LegacyDeploymentPurposes.Unbox(method.Invoke(
            null,
            [itemId, deploymentName, connectionSelector, sourceName, profileDeploymentTypesById, profileDeploymentTypesByName, legacyConnections])!);
    }

    private static AIProviderConnection CreateConnection(
        string itemId,
        string name,
        string clientName,
        string legacyEmbeddingDeploymentName = null,
        string legacyUtilityDeploymentName = null)
    {
        var connection = new AIProviderConnection
        {
            ItemId = itemId,
            Name = name,
            ClientName = clientName,
        };

        connection.SetLegacyEmbeddingDeploymentName(legacyEmbeddingDeploymentName);
        connection.SetLegacyUtilityDeploymentName(legacyUtilityDeploymentName);

        return connection;
    }
}
