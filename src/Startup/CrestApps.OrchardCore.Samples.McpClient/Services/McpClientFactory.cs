using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Samples.McpClient.Services;

/// <summary>
/// Represents the mcp client factory.
/// </summary>
public sealed class McpClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpClientFactory"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public McpClientFactory(
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<ModelContextProtocol.Client.McpClient> CreateAsync(CancellationToken cancellationToken)
    {
        var endpoint = _configuration["Mcp:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Mcp:Endpoint is not configured.");
        }

        var transportOptions = new HttpClientTransportOptions
        {
            Endpoint = new Uri(endpoint),
            TransportMode = HttpTransportMode.AutoDetect,
        };

        var apiKey = _configuration["Mcp:ApiKey"];

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            transportOptions.AdditionalHeaders = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {apiKey}",
            };
        }

        var transport = new HttpClientTransport(transportOptions, _loggerFactory);

        var clientOptions = new McpClientOptions
        {
            ClientInfo = new Implementation
            {
                Name = "CrestApps.OrchardCore.Samples.McpClient",
                Version = "1.0.0",
            },
        };

        try
        {
            return await ModelContextProtocol.Client.McpClient.CreateAsync(transport, clientOptions, _loggerFactory, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"""
                The MCP server at '{endpoint}' returned a 404 Not Found response.
                Use the MCP base endpoint (for example, 'https://localhost:5001/mcp') instead of a transport-specific path.
                Please ensure the MCP Server feature is enabled on the default tenant in the Orchard Core admin dashboard (Configuration > Features > search for 'MCP Server').
                """,
                ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                $"""
                The MCP server at '{endpoint}' returned a 401 Unauthorized response.
                The server requires authentication. Configure the 'Mcp:ApiKey' setting in appsettings.json with a valid API key, and ensure the MCP server's authentication type is set to 'ApiKey' with a matching key in the Orchard Core admin dashboard.
                """,
                ex);
        }
    }
}
