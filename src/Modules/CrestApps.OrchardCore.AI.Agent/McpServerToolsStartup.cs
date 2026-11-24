using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.McpServer;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save and delete the prompts and resources of the site's MCP server, when the MCP
/// server is on.
/// </summary>
[RequireFeatures(McpPermissions.Feature.Server)]
public sealed class McpServerToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpServerToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public McpServerToolsStartup(IStringLocalizer<McpServerToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListMcpPromptsTool>(ListMcpPromptsTool.TheName)
            .WithTitle(S["List MCP Prompts"])
            .WithDescription(S["Lists the prompts the site's MCP server offers, with their arguments and messages."])
            .WithCategory(S["MCP Server"])
            .Selectable();

        services.AddCoreAITool<SaveMcpPromptTool>(SaveMcpPromptTool.TheName)
            .WithTitle(S["Save MCP Prompt"])
            .WithDescription(S["Creates or updates an MCP server prompt: name, title, description, arguments and the user and assistant messages."])
            .WithCategory(S["MCP Server"])
            .Selectable();

        services.AddCoreAITool<DeleteMcpPromptTool>(DeleteMcpPromptTool.TheName)
            .WithTitle(S["Delete MCP Prompts"])
            .WithDescription(S["Deletes MCP server prompts by id."])
            .WithCategory(S["MCP Server"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListMcpResourcesTool>(ListMcpResourcesTool.TheName)
            .WithTitle(S["List MCP Resources"])
            .WithDescription(S["Lists the resources the site's MCP server offers, with type, URI pattern and MIME type, and whether credentials are stored. With includeTypes it lists the resource types and the URI variables each supports."])
            .WithCategory(S["MCP Server"])
            .Selectable();

        services.AddCoreAITool<SaveMcpResourceTool>(SaveMcpResourceTool.TheName)
            .WithTitle(S["Save MCP Resource"])
            .WithDescription(S["Creates or updates an MCP server resource of a given type and URI pattern. A resource makes files, media, content or a remote FTP/SFTP folder readable by MCP clients, so confirm what it exposes. Credentials are never taken: FTP and SFTP resources, and file resources (which read files of the server), are created and changed on the admin page."])
            .WithCategory(S["MCP Server"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteMcpResourceTool>(DeleteMcpResourceTool.TheName)
            .WithTitle(S["Delete MCP Resources"])
            .WithDescription(S["Deletes MCP server resources by id."])
            .WithCategory(S["MCP Server"])
            .RequireApproval()
            .Selectable();
    }
}
