using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIProfileV1DocumentMigrationsTests
{
    [Fact]
    public void NormalizePersistedProfileDocument_WhenLegacyNestedPropertiesExist_ShouldFlattenAndRenameLegacyKeys()
    {
        // Arrange
        var profileDocument = new JsonObject
        {
            [nameof(AIProfile.Name)] = "test",
            [nameof(AIProfile.Properties)] = new JsonObject
            {
                [nameof(AIProfileMetadata)] = new JsonObject
                {
                    [nameof(AIProfileMetadata.SystemMessage)] = "system message into",
                },
                ["AIProfileFunctionInvocationMetadata"] = new JsonObject
                {
                    [nameof(FunctionInvocationMetadata.Names)] = new JsonArray("getUserInfo"),
                },
                ["AIProfileDataSourceMetadata"] = new JsonObject
                {
                    ["DataSourceId"] = "data-source-1",
                },
            },
        };

        // Act
        var updated = InvokeNormalizePersistedProfileDocument(profileDocument);

        // Assert
        Assert.True(updated);
        Assert.DoesNotContain(nameof(AIProfile.Properties), profileDocument.Select(property => property.Key), StringComparer.Ordinal);
        Assert.DoesNotContain("AIProfileFunctionInvocationMetadata", profileDocument.Select(property => property.Key), StringComparer.Ordinal);
        Assert.DoesNotContain("AIProfileDataSourceMetadata", profileDocument.Select(property => property.Key), StringComparer.Ordinal);
        Assert.NotNull(profileDocument[nameof(AIProfileMetadata)]);
        Assert.NotNull(profileDocument["DataSourceMetadata"]);
        Assert.NotNull(profileDocument[nameof(FunctionInvocationMetadata)]);
    }

    private static bool InvokeNormalizePersistedProfileDocument(JsonObject profileDocument)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.AIProfileV1DocumentMigrations", throwOnError: true)!
            .GetMethod("NormalizePersistedProfileDocument", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [profileDocument])!;
    }
}
