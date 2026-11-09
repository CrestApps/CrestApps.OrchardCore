using System.Text.Encodings.Web;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Core.ApiKeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

/// <summary>
/// Signs an MCP client in with the MCP server's configured API key, through the shared
/// <see cref="ConfiguredApiKeyAuthenticationHandler{TOptions}"/>.
/// </summary>
internal sealed class McpApiKeyAuthenticationHandler : ConfiguredApiKeyAuthenticationHandler<McpApiKeyAuthenticationOptions>
{
    private readonly IOptionsMonitor<McpServerOptions> _mcpServerOptionsMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpApiKeyAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="mcpServerOptionsMonitor">The MCP server options monitor.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="encoder">The encoder.</param>
    public McpApiKeyAuthenticationHandler(
        IOptionsMonitor<McpApiKeyAuthenticationOptions> options,
        IOptionsMonitor<McpServerOptions> mcpServerOptionsMonitor,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder)
    {
        _mcpServerOptionsMonitor = mcpServerOptionsMonitor;
    }

    protected override bool IsApiKeyAuthenticationEnabled
        => _mcpServerOptionsMonitor.CurrentValue.AuthenticationType == McpServerAuthenticationType.ApiKey;

    protected override string ConfiguredApiKey => _mcpServerOptionsMonitor.CurrentValue.ApiKey;

    protected override string CallerId => "mcp-api-key-user";

    protected override string CallerName => "MCP API Key User";
}

internal sealed class McpApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

internal static class McpApiKeyAuthenticationDefaults
{
    /// <summary>
    /// The authentication scheme constant.
    /// </summary>
    public const string AuthenticationScheme = "McpApiKey";
}
