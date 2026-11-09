using CrestApps.OrchardCore.AI.Core.ViewModels;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

/// <summary>
/// Represents the view model for SSE MCP connection fields: the server endpoint and its authentication.
/// </summary>
public class SseConnectionFieldsViewModel : ConnectionAuthFieldsViewModel
{
    /// <summary>
    /// Gets or sets the endpoint.
    /// </summary>
    public string Endpoint { get; set; }
}
