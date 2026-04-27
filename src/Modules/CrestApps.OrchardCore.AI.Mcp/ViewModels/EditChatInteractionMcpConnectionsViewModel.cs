using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

/// <summary>
/// Represents the view model for chat interaction mcp connections.
/// </summary>
public class ChatInteractionMcpConnectionsViewModel
{
    /// <summary>
    /// Gets or sets the connections.
    /// </summary>
    public ToolEntry[] Connections { get; set; }
}
