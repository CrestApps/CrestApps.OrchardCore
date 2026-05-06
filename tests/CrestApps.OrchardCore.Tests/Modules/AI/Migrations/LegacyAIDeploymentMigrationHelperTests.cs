using System.Reflection;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;

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

    private static Type GetHelperType()
        => typeof(Startup).Assembly.GetType(
            "CrestApps.OrchardCore.AI.Migrations.LegacyAIDeploymentMigrationHelper",
            throwOnError: true)!;
}
