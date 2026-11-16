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
    /// <c>npx</c> or <c>uvx</c>. Empty means any command, which is the behavior of earlier versions. When set, a connection
    /// may set only the environment variables <see cref="AllowedEnvironmentVariables"/> lists. The arguments and the
    /// working directory are not limited.
    /// </summary>
    public string[] AllowedCommands { get; set; } = [];

    /// <summary>
    /// Gets or sets the environment variables a local connection may set while <see cref="AllowedCommands"/> is set,
    /// compared without case, such as <c>GITHUB_TOKEN</c>. An entry ending with <c>*</c> allows every name that starts
    /// with the rest, such as <c>GITHUB_*</c>. Empty means a connection may set none. A variable that changes which
    /// program runs or what it loads (<c>PATH</c>, <c>NODE_OPTIONS</c>, <c>LD_PRELOAD</c> and the like) is allowed only
    /// when it is listed by its full name, never by a <c>*</c> entry.
    /// </summary>
    public string[] AllowedEnvironmentVariables { get; set; } = [];
}
