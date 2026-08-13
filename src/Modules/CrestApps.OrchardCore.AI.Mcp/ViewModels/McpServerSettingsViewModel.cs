using CrestApps.Core.AI.Mcp.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

/// <summary>
/// Represents the editable MCP server settings shown on the site settings page.
/// </summary>
public class McpServerSettingsViewModel
{
    /// <summary>
    /// Gets or sets the authentication type used by the MCP server.
    /// </summary>
    public McpServerAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the API key used when the authentication type is <c>ApiKey</c>.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>AccessMcpServer</c> permission is required.
    /// </summary>
    public bool RequireAccessPermission { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every non-hidden tool and tool instance is exposed to clients.
    /// </summary>
    public bool ExposeAllTools { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key is already stored.
    /// </summary>
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets the selectable authentication types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AuthenticationTypes { get; set; }
}
