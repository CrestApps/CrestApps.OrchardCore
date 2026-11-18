using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Connections;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save and delete MCP connections, when the MCP client is on. Credentials are never
/// taken or shown, local (stdio) connections are never created or changed, and the connections the running assistant
/// uses cannot be changed from a chat.
/// </summary>
[RequireFeatures(McpPermissions.Feature.Area)]
public sealed class McpConnectionsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpConnectionsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpConnectionsToolsStartup(IStringLocalizer<McpConnectionsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListMcpConnectionsTool>(ListMcpConnectionsTool.TheName)
            .WithTitle(S["List MCP Connections"])
            .WithDescription(S["Lists MCP connections with transport, title, endpoint or command, authentication type, which credentials are stored (present or absent only) and which profiles use them."])
            .WithCategory(S["MCP"])
            .Selectable();

        services.AddCoreAITool<SaveMcpConnectionTool>(SaveMcpConnectionTool.TheName)
            .WithTitle(S["Save MCP Connection"])
            .WithDescription(S["Creates or updates a remote (SSE) MCP connection: title, endpoint and authentication. Credentials are never taken: a person enters them on the admin page. Tools of a connected server run with whatever that server allows, so confirm the address with the user. Local-process (stdio) connections cannot be created or changed with this tool."])
            .WithCategory(S["MCP"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteMcpConnectionTool>(DeleteMcpConnectionTool.TheName)
            .WithTitle(S["Delete MCP Connections"])
            .WithDescription(S["Deletes MCP connections by id. Profiles that used them lose those tools."])
            .WithCategory(S["MCP"])
            .RequireApproval()
            .Selectable();
    }
}
