using System.Reflection;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIDeploymentTypeMigrationsTests
{
    [Fact]
    public void FindDefaultChatDeploymentId_WhenDefaultDeploymentExists_ShouldReturnDefaultDeploymentId()
    {
        var profile = CreateProfile("OpenAI", "legacy-connection");
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "chat-secondary",
                ProviderName = "OpenAI",
                ConnectionName = "legacy-connection",
                Type = AIDeploymentType.Chat,
            },
            new AIDeployment
            {
                ItemId = "chat-default",
                ProviderName = "OpenAI",
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
        var profile = CreateProfile("OpenAI", "Friendly Connection");
        var deployments = new[]
        {
            new AIDeployment
            {
                ItemId = "embedding-default",
                ProviderName = "OpenAI",
                ConnectionName = "legacy-connection",
                ConnectionNameAlias = "Friendly Connection",
                Type = AIDeploymentType.Embedding,
                IsDefault = true,
            },
            new AIDeployment
            {
                ItemId = "chat-first",
                ProviderName = "OpenAI",
                ConnectionName = "legacy-connection",
                ConnectionNameAlias = "Friendly Connection",
                Type = AIDeploymentType.Chat,
            },
        };

        var result = InvokeFindDefaultChatDeploymentId(profile, deployments);

        Assert.Equal("chat-first", result);
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

    private static AIProfile CreateProfile(string source, string connectionName)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return new AIProfile
        {
            Source = source,
            ConnectionName = connectionName,
        };
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
