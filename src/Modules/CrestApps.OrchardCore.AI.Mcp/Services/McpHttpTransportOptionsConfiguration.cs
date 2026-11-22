using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Picks the session mode of the MCP server's Streamable HTTP transport.
/// </summary>
/// <remarks>
/// The MCP SDK defaults to <see cref="HttpServerSessionMode.Stateless"/>, which does not map the <c>GET</c> and
/// <c>DELETE</c> endpoints of <c>/mcp</c>. The server defaults to
/// <see cref="HttpServerSessionMode.StatefulForInitializeClients"/> instead, so clients that use the
/// <c>initialize</c> handshake get a session and can open the server-to-client stream and end their session,
/// while clients on newer protocol revisions are still served statelessly. The
/// <c>CrestApps:AI:McpServer:SessionMode</c> shell configuration value picks another mode, for example
/// <see cref="HttpServerSessionMode.Stateless"/> when the site runs on several nodes without session affinity.
/// </remarks>
internal sealed class McpHttpTransportOptionsConfiguration : IConfigureOptions<HttpServerTransportOptions>
{
    /// <summary>
    /// The shell configuration key that holds the session mode.
    /// </summary>
    public const string SessionModeKey = "CrestApps:AI:McpServer:SessionMode";

    private readonly IShellConfiguration _shellConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpHttpTransportOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="shellConfiguration">The shell configuration.</param>
    public McpHttpTransportOptionsConfiguration(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    /// <summary>
    /// Sets the session mode from the shell configuration, or to
    /// <see cref="HttpServerSessionMode.StatefulForInitializeClients"/> when none, or an unknown one, is configured.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(HttpServerTransportOptions options)
    {
        options.SessionMode = Enum.TryParse<HttpServerSessionMode>(_shellConfiguration[SessionModeKey], ignoreCase: true, out var sessionMode)
            && Enum.IsDefined(sessionMode)
            ? sessionMode
            : HttpServerSessionMode.StatefulForInitializeClients;
    }
}
