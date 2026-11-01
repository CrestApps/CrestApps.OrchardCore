namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The WebMCP settings of an AI profile.
/// </summary>
public sealed class WebMcpProfileSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the profile's chat may call the WebMCP tools on the page the user has
    /// open. The default is <see langword="false"/>.
    /// </summary>
    public bool AllowPageTools { get; set; }
}
