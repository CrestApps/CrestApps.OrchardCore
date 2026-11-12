namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Host settings for local (stdio) MCP connections, bound from the <c>CrestApps:AI:Mcp:Stdio</c> configuration
/// section. They come from configuration the host's operator controls (appsettings, environment variables), never
/// from a setting a tenant's administrator can edit.
/// </summary>
public sealed class McpStdioOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps:AI:Mcp:Stdio";

    /// <summary>
    /// Gets or sets the commands a local connection may start, compared without case and by file name, such as
    /// <c>npx</c> or <c>uvx</c>. Empty means any command, which is the behavior of earlier versions.
    /// </summary>
    public string[] AllowedCommands { get; set; } = [];
}
