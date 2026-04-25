using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

public sealed class AIChatSessionV1PromptDataMigrationsTests
{
    [Fact]
    public void NormalizePersistedSessionDocument_WhenLastActivityUtcIsMissing_ShouldBackfillItFromCreatedUtc()
    {
        // Arrange
        var sessionDocument = new JsonObject
        {
            [nameof(AIChatSession.SessionId)] = "session-1",
            [nameof(AIChatSession.CreatedUtc)] = "2026-04-20T22:14:45Z",
        };

        // Act
        var updated = InvokeNormalizePersistedSessionDocument(sessionDocument);

        // Assert
        Assert.True(updated);
        Assert.Equal(
            sessionDocument[nameof(AIChatSession.CreatedUtc)]?.ToJsonString(),
            sessionDocument[nameof(AIChatSession.LastActivityUtc)]?.ToJsonString());
    }

    [Fact]
    public void NormalizePersistedSessionDocument_WhenLastActivityUtcAlreadyExists_ShouldLeaveDocumentUnchanged()
    {
        // Arrange
        var sessionDocument = new JsonObject
        {
            [nameof(AIChatSession.SessionId)] = "session-1",
            [nameof(AIChatSession.CreatedUtc)] = "2026-04-20T22:14:45Z",
            [nameof(AIChatSession.LastActivityUtc)] = "2026-04-20T22:16:00Z",
        };

        // Act
        var updated = InvokeNormalizePersistedSessionDocument(sessionDocument);

        // Assert
        Assert.False(updated);
        Assert.Equal("\"2026-04-20T22:16:00Z\"", sessionDocument[nameof(AIChatSession.LastActivityUtc)]?.ToJsonString());
    }

    private static bool InvokeNormalizePersistedSessionDocument(JsonObject sessionDocument)
    {
        var method = typeof(Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Migrations.AIChatSessionV1PromptDataMigrations", throwOnError: true)!
            .GetMethod("NormalizePersistedSessionDocument", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (bool)method.Invoke(null, [sessionDocument])!;
    }
}
