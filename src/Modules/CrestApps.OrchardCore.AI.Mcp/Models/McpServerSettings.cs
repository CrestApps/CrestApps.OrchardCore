using CrestApps.Core.AI.Mcp.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Models;

/// <summary>
/// The site settings that configure the MCP server feature. The values are loaded into
/// <see cref="McpServerOptions"/> by <see cref="Services.McpServerOptionsConfiguration"/>, so an operator can
/// configure authentication and control which tools are exposed to MCP clients from the admin UI.
/// </summary>
public sealed class McpServerSettings
{
    /// <summary>
    /// Gets or sets the authentication type used by the MCP server.
    /// </summary>
    public McpServerAuthenticationType AuthenticationType { get; set; } = McpServerAuthenticationType.OpenId;

    /// <summary>
    /// Gets or sets the API key required when <see cref="AuthenticationType"/> is
    /// <see cref="McpServerAuthenticationType.ApiKey"/>.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>AccessMcpServer</c> permission is required. Applies
    /// only when <see cref="AuthenticationType"/> is <see cref="McpServerAuthenticationType.OpenId"/>.
    /// </summary>
    public bool RequireAccessPermission { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether every non-hidden tool and configured tool instance is exposed
    /// to MCP clients. When <c>false</c> (the default), only the tools listed in <see cref="Tools"/> are
    /// exposed. When <c>true</c>, <see cref="Tools"/> is ignored and everything is exposed.
    /// </summary>
    public bool ExposeAllTools { get; set; }

    /// <summary>
    /// Gets or sets the allow-list of tool names and tool instance names exposed to MCP clients when
    /// <see cref="ExposeAllTools"/> is <c>false</c>.
    /// </summary>
    public IList<string> Tools { get; set; } = [];
}
