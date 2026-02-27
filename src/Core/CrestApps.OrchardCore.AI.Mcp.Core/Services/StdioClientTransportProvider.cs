using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Client;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Core.Services;

public sealed class StdioClientTransportProvider : IMcpClientTransportProvider
{
    public bool CanHandle(McpConnection connection)
        => connection.Source == McpConstants.TransportTypes.StdIo;

    public Task<IClientTransport> GetAsync(McpConnection connection)
    {
        var metadata = connection.As<StdioMcpConnectionMetadata>();

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = connection.DisplayText,
            Command = metadata.Command,
            Arguments = metadata.Arguments,
            WorkingDirectory = metadata.WorkingDirectory,
            EnvironmentVariables = metadata.EnvironmentVariables,
        });

        return Task.FromResult<IClientTransport>(transport);
    }
}
