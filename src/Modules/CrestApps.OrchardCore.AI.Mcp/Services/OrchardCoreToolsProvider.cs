using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Provides AI tools from Orchard Core to be exposed via the MCP server.
/// This service discovers and returns AI tools registered in the system.
/// </summary>
public sealed class OrchardCoreToolsProvider : IEnumerable<McpServerTool>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IServiceProvider _serviceProvider;
    private List<McpServerTool> _functions;

    public OrchardCoreToolsProvider(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IServiceProvider serviceProvider)
    {
        _toolDefinitions = toolDefinitions.Value;
        _serviceProvider = serviceProvider;
    }

    public IEnumerator<McpServerTool> GetEnumerator()
    {
        EnsureFunctionsInitialized();

        return _functions.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private void EnsureFunctionsInitialized()
    {
        if (_functions is not null)
        {
            return;
        }

        _functions = [];

        // Get tools from tool definitions (registered via services.AddAITool<T>)
        foreach (var definition in _toolDefinitions.Tools.Values)
        {
            if (ActivatorUtilities.CreateInstance(_serviceProvider, definition.ToolType) is AIFunction aiFunction)
            {
                _functions.Add(McpServerTool.Create(aiFunction));
            }
        }
    }
}
