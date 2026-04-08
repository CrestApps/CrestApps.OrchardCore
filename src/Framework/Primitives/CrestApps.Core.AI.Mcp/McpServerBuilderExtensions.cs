using CrestApps.Core.AI.Mcp.Services;
using CrestApps.Core.AI.Tooling;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.Core.AI.Mcp;

public static class McpServerBuilderExtensions
{
    /// <summary>
    /// Registers the standard CrestApps MCP server handlers for tools, prompts, and resources.
    /// This wires the CrestApps tool registry (<see cref="AIToolDefinitionOptions"/>),
    /// <see cref="IMcpServerPromptService"/>, and <see cref="IMcpServerResourceService"/>
    /// into the MCP protocol so both Orchard Core and standalone MVC hosts share the same handler logic.
    /// </summary>
    public static IMcpServerBuilder WithCrestAppsHandlers(this IMcpServerBuilder builder)
    {
        return builder
            .WithListToolsHandler((request, cancellationToken) =>
            {
                var toolDefinitions = request.Services.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value;
                ILogger logger = null;
                var tools = new List<Tool>();

                foreach (var (name, _) in toolDefinitions.Tools)
                {
                    try
                    {
                        if (request.Services.GetKeyedService<AITool>(name) is AIFunction aiFunction)
                        {
                            tools.Add(new Tool
                            {
                                Name = aiFunction.Name,
                                Description = aiFunction.Description,
                                InputSchema = aiFunction.JsonSchema,
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        logger ??= request.Services.GetRequiredService<ILogger<IMcpServerPromptService>>();
                        logger.LogError(ex, "Error creating tool instance for '{ToolName}'.", name);
                    }
                }

                var sdkTools = request.Services.GetService<IEnumerable<McpServerTool>>();

                if (sdkTools is not null)
                {
                    foreach (var sdkTool in sdkTools)
                    {
                        if (!tools.Any(t => t.Name == sdkTool.ProtocolTool.Name))
                        {
                            tools.Add(sdkTool.ProtocolTool);
                        }
                    }
                }

                return ValueTask.FromResult(new ListToolsResult { Tools = tools });
            })
            .WithCallToolHandler(async (request, cancellationToken) =>
            {
                var toolDefinitions = request.Services.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value;

                if (toolDefinitions.Tools.ContainsKey(request.Params.Name))
                {
                    if (request.Services.GetKeyedService<AITool>(request.Params.Name) is not AIFunction aiFunction)
                    {
                        throw new McpException($"Failed to create tool '{request.Params.Name}'.");
                    }

                    var arguments = new AIFunctionArguments
                    {
                        Services = request.Services,
                        Context = new Dictionary<object, object>
                        {
                            ["mcpRequest"] = request,
                        },
                    };

                    if (request.Params.Arguments is not null)
                    {
                        foreach (var kvp in request.Params.Arguments)
                        {
                            arguments[kvp.Key] = kvp.Value;
                        }
                    }

                    var result = await aiFunction.InvokeAsync(arguments, cancellationToken);

                    return new CallToolResult
                    {
                        Content = [new TextContentBlock { Text = result?.ToString() ?? string.Empty }],
                    };
                }

                var sdkTools = request.Services.GetService<IEnumerable<McpServerTool>>();
                var sdkTool = sdkTools?.FirstOrDefault(t => t.ProtocolTool.Name == request.Params.Name);

                if (sdkTool is not null)
                {
                    return await sdkTool.InvokeAsync(request, cancellationToken);
                }

                throw new McpException($"Tool '{request.Params.Name}' not found.");
            })
            .WithListPromptsHandler(async (request, cancellationToken) =>
            {
                var promptService = request.Services.GetRequiredService<IMcpServerPromptService>();

                return new ListPromptsResult
                {
                    Prompts = await promptService.ListAsync(),
                };
            })
            .WithGetPromptHandler(async (request, cancellationToken) =>
            {
                var promptService = request.Services.GetRequiredService<IMcpServerPromptService>();

                return await promptService.GetAsync(request, cancellationToken);
            })
            .WithListResourcesHandler(async (request, cancellationToken) =>
            {
                var resourceService = request.Services.GetRequiredService<IMcpServerResourceService>();

                return new ListResourcesResult
                {
                    Resources = await resourceService.ListAsync(),
                };
            })
            .WithListResourceTemplatesHandler(async (request, cancellationToken) =>
            {
                var resourceService = request.Services.GetRequiredService<IMcpServerResourceService>();

                return new ListResourceTemplatesResult
                {
                    ResourceTemplates = await resourceService.ListTemplatesAsync(),
                };
            })
            .WithReadResourceHandler(async (request, cancellationToken) =>
            {
                var resourceService = request.Services.GetRequiredService<IMcpServerResourceService>();

                return await resourceService.ReadAsync(request, cancellationToken);
            });
    }
}
