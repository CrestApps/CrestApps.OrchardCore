using System.Globalization;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Agent;
using CrestApps.OrchardCore.AI.Agent.ApiKeys;
using CrestApps.OrchardCore.AI.Agent.ChatInteractions;
using CrestApps.OrchardCore.AI.Agent.ChatSessions;
using CrestApps.OrchardCore.AI.Agent.Connections;
using CrestApps.OrchardCore.AI.Agent.McpServer;
using CrestApps.OrchardCore.AI.Agent.Memory;
using CrestApps.OrchardCore.AI.Agent.Usage;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Agent;

/// <summary>
/// The API key, conversation, usage, chat interaction, memory, MCP and A2A tools are registered with a title, a
/// description and a category, can be picked on a profile, ask for approval exactly where the tool coverage plan says
/// so, and are registered by the startup of the feature they belong to.
/// </summary>
public sealed class AIOperationsToolRegistrationTests
{
    public static TheoryData<string, bool> Tools => new()
    {
        { ListAIApiKeysTool.TheName, false },
        { SaveAIApiKeyTool.TheName, true },
        { DeleteAIApiKeyTool.TheName, true },
        { DeleteAIConversationTool.TheName, true },
        { DeleteAllAIConversationsTool.TheName, true },
        { QueryAIUsageTool.TheName, false },
        { ListChatInteractionsTool.TheName, false },
        { DeleteChatInteractionsTool.TheName, true },
        { ListMcpConnectionsTool.TheName, false },
        { SaveMcpConnectionTool.TheName, true },
        { DeleteMcpConnectionTool.TheName, true },
        { ListMcpPromptsTool.TheName, false },
        { SaveMcpPromptTool.TheName, false },
        { DeleteMcpPromptTool.TheName, true },
        { ListMcpResourcesTool.TheName, false },
        { SaveMcpResourceTool.TheName, true },
        { DeleteMcpResourceTool.TheName, true },
        { ListA2AConnectionsTool.TheName, false },
        { SaveA2AConnectionTool.TheName, true },
        { DeleteA2AConnectionTool.TheName, true },
        { ClearAIUserMemoryTool.TheName, true },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public void Tool_IsRegisteredWithItsTextsAndApproval(string toolName, bool requiresApproval)
    {
        // Arrange
        var tools = GetTools();

        // Act
        var found = tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(toolName, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.NotEqual(toolName, tool.Description);
        Assert.False(string.IsNullOrWhiteSpace(tool.Category));
        Assert.True(tool.IsSelectable());
        Assert.Equal(requiresApproval, tool.RequiresApproval);
    }

    [Theory]
    [InlineData(typeof(AIApiKeysToolsStartup), 3, AIConstants.Feature.ChatApi)]
    [InlineData(typeof(AIChatSessionsToolsStartup), 2, AIConstants.Feature.Chat)]
    [InlineData(typeof(AIUsageToolsStartup), 1, AIConstants.Feature.UsageAnalytics)]
    [InlineData(typeof(ChatInteractionsToolsStartup), 2, AIConstants.Feature.ChatInteractions)]
    [InlineData(typeof(McpConnectionsToolsStartup), 3, McpPermissions.Feature.Area)]
    [InlineData(typeof(McpServerToolsStartup), 6, McpPermissions.Feature.Server)]
    [InlineData(typeof(A2AConnectionsToolsStartup), 3, AIConstants.Feature.A2A)]
    [InlineData(typeof(AIMemoryToolsStartup), 1, MemoryConstants.Feature.Memory)]
    public void EachFeatureStartup_RegistersItsToolsAndRequiresItsFeature(Type startupType, int expectedCount, string featureId)
    {
        // Act
        var tools = GetTools(startupType);
        var attribute = startupType.GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();

        // Assert
        Assert.Equal(expectedCount, tools.Count);
        Assert.Contains(featureId, attribute.RequiredFeatureNames);
    }

    [Fact]
    public void NamesReadFromModulesThisModuleCannotReference_MatchTheModules()
    {
        // The AI Core constants, the mirrored permissions and the keys read by name stand in for module types the
        // AI Agent module cannot use.
        Assert.Equal(CrestApps.OrchardCore.AI.A2A.A2AConstants.Feature.Area, AIConstants.Feature.A2A);
        Assert.Equal(CrestApps.OrchardCore.AI.Chat.Interactions.Core.ChatInteractionsConstants.Feature.ChatInteractions, AIConstants.Feature.ChatInteractions);
        Assert.Equal(CrestApps.OrchardCore.AI.A2A.A2APermissions.ManageA2AConnections.Name, AIModulePermissions.ManageA2AConnections.Name);
        Assert.Equal(CrestApps.OrchardCore.AI.Chat.Services.ChatAnalyticsPermissionProvider.ViewChatAnalytics.Name, AIModulePermissions.ViewChatAnalytics.Name);
        Assert.Equal(nameof(CrestApps.OrchardCore.AI.Chat.Interactions.Models.ProfileBuilderInteractionMetadata), ChatInteractionToolSupport.ProfileBuilderMetadataKey);
        Assert.Equal([CrestApps.Core.AI.Ftp.FtpResourceConstants.Type, CrestApps.Core.AI.Sftp.SftpResourceConstants.Type], McpServerToolSupport.RemoteResourceTypes);
        Assert.Equal([nameof(CrestApps.Core.AI.Ftp.Models.FtpConnectionMetadata), nameof(CrestApps.Core.AI.Sftp.Models.SftpConnectionMetadata)], McpServerToolSupport.RemoteConnectionKeys);
    }

    private static IReadOnlyDictionary<string, AIToolDefinitionEntry> GetTools(Type startupType = null)
    {
        var services = new ServiceCollection();

        foreach (var type in typeof(CrestApps.OrchardCore.AI.Agent.Startup).Assembly.GetTypes()
            .Where(type => typeof(StartupBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => startupType is null || type == startupType))
        {
            var constructor = type.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(parameter.ParameterType.GetGenericArguments()[0])))
                .ToArray();

            ((StartupBase)constructor.Invoke(arguments)).ConfigureServices(services);
        }

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
