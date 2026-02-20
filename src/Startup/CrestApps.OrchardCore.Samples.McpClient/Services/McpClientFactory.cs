using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Samples.McpClient.Services;

public sealed class McpClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;

    public McpClientFactory(
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

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
            TransportMode = HttpTransportMode.Sse,
        };

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
                $"The MCP server at '{endpoint}' returned a 404 Not Found response. " +
                "Please ensure the MCP Server feature is enabled on the default tenant in the Orchard Core admin dashboard " +
                "(Configuration > Features > search for 'MCP Server').", ex);
        }
    }
}
