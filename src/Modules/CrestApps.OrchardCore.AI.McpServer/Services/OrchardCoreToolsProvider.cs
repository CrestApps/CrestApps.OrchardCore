using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AI.McpServer.Services;

/// <summary>
/// Provides AI tools from Orchard Core to be exposed via the MCP server.
/// This service discovers and returns AI tools registered in the system.
/// </summary>
public sealed class OrchardCoreToolsProvider : IEnumerable<McpServerTool>
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<McpServerTool> _tools;

    public OrchardCoreToolsProvider(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IServiceProvider serviceProvider)
    {
        _toolDefinitions = toolDefinitions.Value;
        _serviceProvider = serviceProvider;
        _tools = [];

        InitializeTools();
    }

    private void InitializeTools()
    {
        // Get tools from tool definitions (registered via services.AddAITool<T>)
        foreach (var (name, definition) in _toolDefinitions.Tools)
        {
            var aiTool = ActivatorUtilities.CreateInstance(_serviceProvider, definition.ToolType) as AITool;

            if (aiTool is AIFunction aiFunction)
            {
                _tools.Add(McpServerTool.Create(aiFunction));
            }
        }
    }

    public IEnumerator<McpServerTool> GetEnumerator()
    {
        return _tools.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
