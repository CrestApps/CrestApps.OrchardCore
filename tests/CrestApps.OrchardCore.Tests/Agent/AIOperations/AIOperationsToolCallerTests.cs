using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Agent.ApiKeys;
using CrestApps.OrchardCore.AI.Agent.ChatInteractions;
using CrestApps.OrchardCore.AI.Agent.ChatSessions;
using CrestApps.OrchardCore.AI.Agent.Connections;
using CrestApps.OrchardCore.AI.Agent.McpServer;
using CrestApps.OrchardCore.AI.Agent.Memory;
using CrestApps.OrchardCore.AI.Agent.Usage;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Tests.Agent.AIOperations;

/// <summary>
/// Every API key, conversation, usage, chat interaction, memory, MCP and A2A tool acts for the person in the chat: it
/// refuses a caller who is not signed in and a caller who lacks the permission the matching admin page asks for, and
/// changes nothing then.
/// </summary>
public sealed class AIOperationsToolCallerTests
{
    public static TheoryData<string, string> Tools => new()
    {
        { ListAIApiKeysTool.TheName, "ManageAIApiKeys" },
        { SaveAIApiKeyTool.TheName, "ManageAIApiKeys" },
        { DeleteAIApiKeyTool.TheName, "ManageAIApiKeys" },
        { DeleteAIConversationTool.TheName, "DeleteChatSession" },
        { DeleteAllAIConversationsTool.TheName, "DeleteAllChatSessions" },
        { QueryAIUsageTool.TheName, "ViewChatAnalytics" },
        { ListChatInteractionsTool.TheName, "ListChatInteractions" },
        { DeleteChatInteractionsTool.TheName, "DeleteOwnChatInteraction" },
        { ListMcpConnectionsTool.TheName, "ManageMcpConnections" },
        { SaveMcpConnectionTool.TheName, "ManageMcpConnections" },
        { DeleteMcpConnectionTool.TheName, "ManageMcpConnections" },
        { ListMcpPromptsTool.TheName, "ManageMcpPrompts" },
        { SaveMcpPromptTool.TheName, "ManageMcpPrompts" },
        { DeleteMcpPromptTool.TheName, "ManageMcpPrompts" },
        { ListMcpResourcesTool.TheName, "ManageMcpResources" },
        { SaveMcpResourceTool.TheName, "ManageMcpResources" },
        { DeleteMcpResourceTool.TheName, "ManageMcpResources" },
        { ListA2AConnectionsTool.TheName, "ManageA2AConnections" },
        { SaveA2AConnectionTool.TheName, "ManageA2AConnections" },
        { DeleteA2AConnectionTool.TheName, "ManageA2AConnections" },
        { ClearAIUserMemoryTool.TheName, "ClearAIMemory" },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesWithoutAnyCheck(string toolName, string permission)
    {
        // Arrange
        _ = permission;

        var harness = CreateHarness();
        harness.SignedIn = false;

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(harness.Checks);
        AssertNothingChanged(harness);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminPage_Refuses(string toolName, string permission)
    {
        // Arrange
        var harness = CreateHarness();
        harness.Authorize = (_, _) => false;

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, harness.Checks[0]);
        AssertNothingChanged(harness);
    }

    private static AIOperationsToolHarness CreateHarness()
    {
        var harness = new AIOperationsToolHarness();

        harness.AddProfile("profile-1", "support");
        harness.AddApiKey("key-1", AIApiKeyKind.Server);
        harness.Sessions.Add(new AIChatSession { SessionId = "session-1", ProfileId = "profile-1", UserId = AgentToolTestContext.UserId });
        harness.Interactions.Add(new ChatInteraction { ItemId = "interaction-1", OwnerId = AgentToolTestContext.UserId, Title = "Draft" });
        harness.Memories.Add(new AIMemoryEntry { ItemId = "memory-1", UserId = AgentToolTestContext.UserId, Name = "likes" });
        harness.AddSseConnection("mcp-1", "GitHub");
        harness.Prompts.Add(new McpPrompt { ItemId = "prompt-1", Name = "summarize", Prompt = new Prompt { Name = "summarize", Title = "Summarize" } });
        harness.Resources.Add(new McpResource { ItemId = "resource-1", Source = "file", DisplayText = "Docs", Resource = new Resource { Uri = "file://resource-1", Name = "docs" } });
        harness.AddA2AConnection("a2a-1", "Partner");

        return harness;
    }

    private static void AssertNothingChanged(AIOperationsToolHarness harness)
    {
        Assert.Empty(harness.UpdatedApiKeys);
        Assert.Empty(harness.DeletedSessions);
        Assert.Empty(harness.DeletedAllProfiles);
        Assert.Empty(harness.Saved);
        Assert.Empty(harness.Deleted);
        Assert.Single(harness.ApiKeys);
        Assert.Single(harness.Memories);
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
        => toolName switch
        {
            ListAIApiKeysTool.TheName => (new ListAIApiKeysTool(), []),
            SaveAIApiKeyTool.TheName => (new SaveAIApiKeyTool(), new() { ["apiKeyId"] = "key-1", ["enabled"] = false }),
            DeleteAIApiKeyTool.TheName => (new DeleteAIApiKeyTool(), new() { ["apiKeyIds"] = new[] { "key-1" } }),
            DeleteAIConversationTool.TheName => (new DeleteAIConversationTool(), new() { ["sessionId"] = "session-1" }),
            DeleteAllAIConversationsTool.TheName => (new DeleteAllAIConversationsTool(), new() { ["profile"] = "profile-1" }),
            QueryAIUsageTool.TheName => (new QueryAIUsageTool(), []),
            ListChatInteractionsTool.TheName => (new ListChatInteractionsTool(), []),
            DeleteChatInteractionsTool.TheName => (new DeleteChatInteractionsTool(), new() { ["interactionIds"] = new[] { "interaction-1" } }),
            ListMcpConnectionsTool.TheName => (new ListMcpConnectionsTool(), []),
            SaveMcpConnectionTool.TheName => (new SaveMcpConnectionTool(), new() { ["connectionId"] = "mcp-1", ["title"] = "Changed" }),
            DeleteMcpConnectionTool.TheName => (new DeleteMcpConnectionTool(), new() { ["connectionIds"] = new[] { "mcp-1" } }),
            ListMcpPromptsTool.TheName => (new ListMcpPromptsTool(), []),
            SaveMcpPromptTool.TheName => (new SaveMcpPromptTool(), new() { ["promptId"] = "prompt-1", ["title"] = "Changed" }),
            DeleteMcpPromptTool.TheName => (new DeleteMcpPromptTool(), new() { ["promptIds"] = new[] { "prompt-1" } }),
            ListMcpResourcesTool.TheName => (new ListMcpResourcesTool(), []),
            SaveMcpResourceTool.TheName => (new SaveMcpResourceTool(), new() { ["resourceId"] = "resource-1", ["title"] = "Changed" }),
            DeleteMcpResourceTool.TheName => (new DeleteMcpResourceTool(), new() { ["resourceIds"] = new[] { "resource-1" } }),
            ListA2AConnectionsTool.TheName => (new ListA2AConnectionsTool(), []),
            SaveA2AConnectionTool.TheName => (new SaveA2AConnectionTool(), new() { ["connectionId"] = "a2a-1", ["title"] = "Changed" }),
            DeleteA2AConnectionTool.TheName => (new DeleteA2AConnectionTool(), new() { ["connectionIds"] = new[] { "a2a-1" } }),
            ClearAIUserMemoryTool.TheName => (new ClearAIUserMemoryTool(), []),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
