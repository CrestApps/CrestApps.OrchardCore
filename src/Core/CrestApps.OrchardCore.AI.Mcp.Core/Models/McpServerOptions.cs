namespace CrestApps.OrchardCore.AI.Mcp.Core.Models;

/// <summary>
/// Configuration options for the MCP server authentication and authorization.
/// </summary>
public sealed class McpServerOptions
{
    /// <summary>
    /// Gets or sets the authentication type to use for the MCP server.
    /// Default is <see cref="McpServerAuthenticationType.OpenId"/>.
    /// </summary>
    public McpServerAuthenticationType AuthenticationType { get; set; } = McpServerAuthenticationType.OpenId;

    /// <summary>
    /// Gets or sets the API key required for authentication when 
    /// <see cref="AuthenticationType"/> is set to <see cref="McpServerAuthenticationType.ApiKey"/>.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets whether to require the <c>AccessMcpServer</c> permission.
    /// When set to <c>false</c>, any authenticated user can access the MCP server.
    /// Default is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// This setting only applies when <see cref="AuthenticationType"/> is 
    /// <see cref="McpServerAuthenticationType.OpenId"/>.
    /// </remarks>
    public bool RequireAccessPermission { get; set; } = true;
}
