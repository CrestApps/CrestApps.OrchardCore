using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIDeploymentV1DocumentMigrationsTests
{
    [Fact]
    public void NormalizeLegacyDeploymentObject_WhenLegacyFieldsAreMissing_ShouldBackfillItemIdModelNameAndType()
    {
        // Arrange
        var deploymentObject = new JsonObject
        {
            [nameof(AIDeployment.Name)] = "gpt-4.1-mini",
            [nameof(AIDeployment.ConnectionName)] = "legacy-connection",
            [nameof(AIDeployment.Source)] = "Azure",
            ["Id"] = "legacy-deployment-id",
        };

        var connections = new[]
        {
            CreateConnection(
                itemId: "legacy-connection",
                name: "Legacy Connection",
                clientName: "Azure",
                legacyChatDeploymentName: "gpt-4.1-mini"),
        };

        // Act
        InvokeNormalizeLegacyDeploymentObject(deploymentObject, "fallback-id", connections);

        // Assert
        Assert.Equal("legacy-deployment-id", deploymentObject[nameof(AIDeployment.ItemId)]?.GetValue<string>());
        Assert.Equal("gpt-4.1-mini", deploymentObject[nameof(AIDeployment.ModelName)]?.GetValue<string>());
        Assert.Equal("Azure", deploymentObject[nameof(AIDeployment.ClientName)]?.GetValue<string>());
        Assert.Equal("Chat", deploymentObject[nameof(AIDeployment.Type)]?.GetValue<string>());
    }

    [Fact]
    public void TryPopulateDefaultDeploymentSettings_WhenUiDefaultExists_ShouldPreferUiDefaultOverLegacyAppSettings()
    {
        // Arrange
        var settings = new DefaultAIDeploymentSettings();
        var connections = new[]
        {
            CreateConnection(
                itemId: "legacy-connection",
                name: "Legacy Connection",
                clientName: "Azure",
                legacyChatDeploymentName: "legacy-default"),
        };
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "ui-default",
                Name = "ui-default",
                ModelName = "ui-default",
                ClientName = "Azure",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat,
                Properties = new Dictionary<string, object> { ["IsDefault"] = true },
            },
            new AIDeployment
            {
                ItemId = "legacy-default-id",
                Name = "legacy-default",
                ModelName = "legacy-default",
                ClientName = "Azure",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat,
            },
        };

        // Act
        var updated = InvokeTryPopulateDefaultDeploymentSettings(settings, connections, deployments);

        // Assert
        Assert.True(updated);
        Assert.Equal("ui-default", settings.DefaultChatDeploymentName);
    }

    [Fact]
    public void TryPopulateDefaultDeploymentSettings_WhenNoUiOrLegacyDefaultExists_ShouldPickFirstChatDeployment()
    {
        // Arrange
        var settings = new DefaultAIDeploymentSettings();
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "chat-b",
                Name = "chat-b",
                ModelName = "chat-b",
                ClientName = "Azure",
                ConnectionName = "b-connection",
                Type = AIDeploymentType.Chat,
            },
            new AIDeployment
            {
                ItemId = "chat-a",
                Name = "chat-a",
                ModelName = "chat-a",
                ClientName = "Azure",
                ConnectionName = "a-connection",
                Type = AIDeploymentType.Chat,
            },
        };

        // Act
        var updated = InvokeTryPopulateDefaultDeploymentSettings(settings, [], deployments);

        // Assert
        Assert.True(updated);
        Assert.Equal("chat-a", settings.DefaultChatDeploymentName);
    }

    private static void InvokeNormalizeLegacyDeploymentObject(
        JsonObject deploymentObject,
        string fallbackItemId,
        IEnumerable<AIProviderConnection> connections)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.AIDeploymentV1DocumentMigrations", throwOnError: true)!
            .GetMethod(
                "NormalizeLegacyDeploymentObject",
                BindingFlags.NonPublic | BindingFlags.Static)!;

        method.Invoke(null, [deploymentObject, fallbackItemId, connections]);
    }

    private static bool InvokeTryPopulateDefaultDeploymentSettings(
        DefaultAIDeploymentSettings settings,
        IEnumerable<AIProviderConnection> connections,
        IEnumerable<AIDeployment> deployments)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.AIDeploymentV1DocumentMigrations", throwOnError: true)!
            .GetMethod(
                "TryPopulateDefaultDeploymentSettings",
                BindingFlags.NonPublic | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [settings, connections, deployments])!;
    }

    private static AIProviderConnection CreateConnection(
        string itemId,
        string name,
        string clientName,
        string legacyChatDeploymentName = null)
    {
        var connection = new AIProviderConnection
        {
            ItemId = itemId,
            Name = name,
            ClientName = clientName,
        };

        connection.SetLegacyChatDeploymentName(legacyChatDeploymentName);

        return connection;
    }
}
