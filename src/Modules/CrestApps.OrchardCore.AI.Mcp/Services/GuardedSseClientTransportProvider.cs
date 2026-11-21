using CrestApps.Core;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// The MCP HTTP (SSE and streamable HTTP) transport for Orchard Core. It sends through the egress-guarded
/// <c>CrestApps.Mcp</c> client, so an endpoint on the site's own network, or a redirect to one, is refused at connect.
/// </summary>
internal sealed class GuardedSseClientTransportProvider : IMcpClientTransportProvider
{
    private readonly IConnectionAuthHeaderBuilder _authHeaderBuilder;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuardedSseClientTransportProvider"/> class.
    /// </summary>
    public GuardedSseClientTransportProvider(
        IConnectionAuthHeaderBuilder authHeaderBuilder,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _authHeaderBuilder = authHeaderBuilder;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public bool CanHandle(McpConnection connection)
        => connection?.Source == McpConstants.TransportTypes.Sse;

    /// <inheritdoc/>
    public async Task<IClientTransport> GetAsync(McpConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!connection.TryGet<SseMcpConnectionMetadata>(out var metadata))
        {
            return null;
        }

        var headers = await _authHeaderBuilder.BuildHeadersAsync(metadata, McpConstants.DataProtectionPurpose);
        var httpClient = _httpClientFactory.CreateClient(McpConstants.HttpClientName);

        return new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = metadata.Endpoint, AdditionalHeaders = headers },
            httpClient,
            _loggerFactory,
            ownsHttpClient: true);
    }
}
